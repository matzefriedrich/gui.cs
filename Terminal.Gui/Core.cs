//
// Core.cs: The core engine for gui.cs
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
// Pending:
//   - Check for NeedDisplay on the hierarchy and repaint
//   - Layout support
//   - "Colors" type or "Attributes" type?
//   - What to surface as "BackgroundCOlor" when clearing a window, an attribute or colors?
//
// Optimziations
//   - Add rendering limitation to the exposed area

namespace Terminal.Gui
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using NStack;

    using Terminal.Gui.Drivers;
    using Terminal.Gui.MonoCurses;
    using Terminal.Gui.Types;

    using Attribute = Terminal.Gui.Drivers.Attribute;

    /// <summary>
    ///     Determines the LayoutStyle for a view, if Absolute, during LayoutSubviews, the
    ///     value from the Frame will be used, if the value is Computer, then the Frame
    ///     will be updated from the X, Y Pos objets and the Width and Heigh Dim objects.
    /// </summary>
    public enum LayoutStyle
    {
        /// <summary>
        ///     The position and size of the view are based on the Frame value.
        /// </summary>
        Absolute,

        /// <summary>
        ///     The position and size of the view will be computed based on the
        ///     X, Y, Width and Height properties and set on the Frame.
        /// </summary>
        Computed
    }

    /// <summary>
    ///     View is the base class for all views on the screen and represents a visible element that can render itself and
    ///     contains zero or more nested views.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The View defines the base functionality for user interface elements in Terminal/gui.cs.  Views
    ///         can contain one or more subviews, can respond to user input and render themselves on the screen.
    ///     </para>
    ///     <para>
    ///         Views can either be created with an absolute position, by calling the constructor that takes a
    ///         Rect parameter to specify the absolute position and size (the Frame of the View) or by setting the
    ///         X, Y, Width and Height properties on the view.    Both approaches use coordinates that are relative
    ///         to the container they are being added to.
    ///     </para>
    ///     <para>
    ///         When you do not specify a Rect frame you can use the more flexible
    ///         Dim and Pos objects that can dynamically update the position of a view.
    ///         The X and Y properties are of type <see cref="T:Terminal.Gui.Pos" />
    ///         and you can use either absolute positions, percentages or anchor
    ///         points.   The Width and Height properties are of type
    ///         <see cref="T:Terminal.Gui.Dim" /> and can use absolute position,
    ///         percentages and anchors.  These are useful as they will take
    ///         care of repositioning your views if your view's frames are resized
    ///         or if the terminal size changes.
    ///     </para>
    ///     <para>
    ///         When you specify the Rect parameter to a view, you are setting the LayoutStyle to Absolute, and the
    ///         view will always stay in the position that you placed it.   To change the position change the
    ///         Frame property to the new position.
    ///     </para>
    ///     <para>
    ///         Subviews can be added to a View by calling the Add method.   The container of a view is the
    ///         Superview.
    ///     </para>
    ///     <para>
    ///         Developers can call the SetNeedsDisplay method on the view to flag a region or the entire view
    ///         as requiring to be redrawn.
    ///     </para>
    ///     <para>
    ///         Views have a ColorScheme property that defines the default colors that subviews
    ///         should use for rendering.   This ensures that the views fit in the context where
    ///         they are being used, and allows for themes to be plugged in.   For example, the
    ///         default colors for windows and toplevels uses a blue background, while it uses
    ///         a white background for dialog boxes and a red background for errors.
    ///     </para>
    ///     <para>
    ///         If a ColorScheme is not set on a view, the result of the ColorScheme is the
    ///         value of the SuperView and the value might only be valid once a view has been
    ///         added to a SuperView, so your subclasses should not rely on ColorScheme being
    ///         set at construction time.
    ///     </para>
    ///     <para>
    ///         Using ColorSchemes has the advantage that your application will work both
    ///         in color as well as black and white displays.
    ///     </para>
    ///     <para>
    ///         Views that are focusable should implement the PositionCursor to make sure that
    ///         the cursor is placed in a location that makes sense.   Unix terminals do not have
    ///         a way of hiding the cursor, so it can be distracting to have the cursor left at
    ///         the last focused view.   So views should make sure that they place the cursor
    ///         in a visually sensible place.
    ///     </para>
    ///     <para>
    ///         The metnod LayoutSubviews is invoked when the size or layout of a view has
    ///         changed.   The default processing system will keep the size and dimensions
    ///         for views that use the LayoutKind.Absolute, and will recompute the
    ///         frames for the vies that use LayoutKind.Computed.
    ///     </para>
    /// </remarks>
    public class View : Responder, IEnumerable
    {
        /// <summary>
        ///     Points to the current driver in use by the view, it is a convenience property
        ///     for simplifying the development of new views.
        /// </summary>
        public static ConsoleDriver Driver = Application.Driver;

        private static readonly IList<View> empty = new List<View>(0).AsReadOnly();

        internal bool childNeedsDisplay;

        private ColorScheme colorScheme;

        // The frame for the object
        private Rect frame;

        private bool layoutNeeded = true;

        private LayoutStyle layoutStyle;

        private List<View> subviews;

        private Dim width, height;

        private Pos x, y;

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:Terminal.Gui.View" /> class with the absolute
        ///     dimensions specified in the frame.   If you want to have Views that can be positioned with
        ///     Pos and Dim properties on X, Y, Width and Height, use the empty constructor.
        /// </summary>
        /// <param name="frame">The region covered by this view.</param>
        public View(Rect frame)
        {
            this.Frame = frame;
            this.CanFocus = false;
            this.LayoutStyle = LayoutStyle.Absolute;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:Terminal.Gui.View" /> class and sets the
        ///     view up for Computed layout, which will use the values in X, Y, Width and Height to
        ///     compute the View's Frame.
        /// </summary>
        public View()
        {
            this.CanFocus = false;
            this.LayoutStyle = LayoutStyle.Computed;
        }

        /// <summary>
        ///     This returns a list of the subviews contained by this view.
        /// </summary>
        /// <value>The subviews.</value>
        public IList<View> Subviews => this.subviews == null ? empty : this.subviews.AsReadOnly();

        internal Rect NeedDisplay { get; private set; } = Rect.Empty;

        /// <summary>
        ///     Gets or sets an identifier for the view;
        /// </summary>
        /// <value>The identifier.</value>
        public ustring Id { get; set; } = "";

        /// <summary>
        ///     Gets or sets a value indicating whether this <see cref="T:Terminal.Gui.View" /> want mouse position reports.
        /// </summary>
        /// <value><c>true</c> if want mouse position reports; otherwise, <c>false</c>.</value>
        public virtual bool WantMousePositionReports { get; set; } = false;

        /// <summary>
        ///     Gets or sets the frame for the view.
        /// </summary>
        /// <value>The frame.</value>
        /// <remarks>
        ///     Altering the Frame of a view will trigger the redrawing of the
        ///     view as well as the redrawing of the affected regions in the superview.
        /// </remarks>
        public virtual Rect Frame
        {
            get => this.frame;
            set
            {
                if (this.SuperView != null)
                {
                    this.SuperView.SetNeedsDisplay(this.frame);
                    this.SuperView.SetNeedsDisplay(value);
                }

                this.frame = value;

                this.SetNeedsLayout();
                this.SetNeedsDisplay(this.frame);
            }
        }

        /// <summary>
        ///     Controls how the view's Frame is computed during the LayoutSubviews method, if Absolute, then
        ///     LayoutSubviews does not change the Frame properties, otherwise the Frame is updated from the
        ///     values in X, Y, Width and Height properties.
        /// </summary>
        /// <value>The layout style.</value>
        public LayoutStyle LayoutStyle
        {
            get => this.layoutStyle;
            set
            {
                this.layoutStyle = value;
                this.SetNeedsLayout();
            }
        }

        /// <summary>
        ///     The bounds represent the View-relative rectangle used for this view.   Updates to the Bounds update the Frame, and
        ///     has the same side effects as updating the frame.
        /// </summary>
        /// <value>The bounds.</value>
        public Rect Bounds
        {
            get => new Rect(Point.Empty, this.Frame.Size);
            set => this.Frame = new Rect(this.frame.Location, value.Size);
        }

        /// <summary>
        ///     Gets or sets the X position for the view (the column).  This is only used when the LayoutStyle is Computed, if the
        ///     LayoutStyle is set to Absolute, this value is ignored.
        /// </summary>
        /// <value>The X Position.</value>
        public Pos X
        {
            get => this.x;
            set
            {
                this.x = value;
                this.SetNeedsLayout();
            }
        }

        /// <summary>
        ///     Gets or sets the Y position for the view (line).  This is only used when the LayoutStyle is Computed, if the
        ///     LayoutStyle is set to Absolute, this value is ignored.
        /// </summary>
        /// <value>The y position (line).</value>
        public Pos Y
        {
            get => this.y;
            set
            {
                this.y = value;
                this.SetNeedsLayout();
            }
        }

        /// <summary>
        ///     Gets or sets the width for the view. This is only used when the LayoutStyle is Computed, if the
        ///     LayoutStyle is set to Absolute, this value is ignored.
        /// </summary>
        /// <value>The width.</value>
        public Dim Width
        {
            get => this.width;
            set
            {
                this.width = value;
                this.SetNeedsLayout();
            }
        }

        /// <summary>
        ///     Gets or sets the height for the view. This is only used when the LayoutStyle is Computed, if the
        ///     LayoutStyle is set to Absolute, this value is ignored.
        /// </summary>
        /// <value>The height.</value>
        public Dim Height
        {
            get => this.height;
            set
            {
                this.height = value;
                this.SetNeedsLayout();
            }
        }

        /// <summary>
        ///     Returns the container for this view, or null if this view has not been added to a container.
        /// </summary>
        /// <value>The super view.</value>
        public View SuperView { get; private set; }

        /// <summary>
        ///     Gets or sets a value indicating whether this <see cref="T:Terminal.Gui.View" /> has focus.
        /// </summary>
        /// <value><c>true</c> if has focus; otherwise, <c>false</c>.</value>
        public override bool HasFocus
        {
            get => base.HasFocus;
            internal set
            {
                if (base.HasFocus != value)
                    this.SetNeedsDisplay();
                base.HasFocus = value;

                // Remove focus down the chain of subviews if focus is removed
                if (value == false && this.Focused != null)
                {
                    this.Focused.HasFocus = false;
                    this.Focused = null;
                }
            }
        }

        /// <summary>
        ///     Returns the currently focused view inside this view, or null if nothing is focused.
        /// </summary>
        /// <value>The focused.</value>
        public View Focused { get; private set; }

        /// <summary>
        ///     Returns the most focused view in the chain of subviews (the leaf view that has the focus).
        /// </summary>
        /// <value>The most focused.</value>
        public View MostFocused
        {
            get
            {
                if (this.Focused == null)
                    return null;
                View most = this.Focused.MostFocused;
                if (most != null)
                    return most;
                return this.Focused;
            }
        }

        /// <summary>
        ///     The color scheme for this view, if it is not defined, it returns the parent's
        ///     color scheme.
        /// </summary>
        public ColorScheme ColorScheme
        {
            get
            {
                if (this.colorScheme == null)
                    return this.SuperView?.ColorScheme;
                return this.colorScheme;
            }
            set => this.colorScheme = value;
        }

        /// <summary>
        ///     Gets an enumerator that enumerates the subviews in this view.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator GetEnumerator()
        {
            foreach (View v in this.Subviews)
                yield return v;
        }

        /// <summary>
        ///     Invoke to flag that this view needs to be redisplayed, by any code
        ///     that alters the state of the view.
        /// </summary>
        public void SetNeedsDisplay()
        {
            this.SetNeedsDisplay(this.Bounds);
        }

        internal void SetNeedsLayout()
        {
            if (this.layoutNeeded)
                return;
            this.layoutNeeded = true;
            if (this.SuperView == null)
                return;
            this.SuperView.layoutNeeded = true;
        }

        /// <summary>
        ///     Flags the specified rectangle region on this view as needing to be repainted.
        /// </summary>
        /// <param name="region">The region that must be flagged for repaint.</param>
        public void SetNeedsDisplay(Rect region)
        {
            if (this.NeedDisplay.IsEmpty)
            {
                this.NeedDisplay = region;
            }
            else
            {
                int x = Math.Min(this.NeedDisplay.X, region.X);
                int y = Math.Min(this.NeedDisplay.Y, region.Y);
                int w = Math.Max(this.NeedDisplay.Width, region.Width);
                int h = Math.Max(this.NeedDisplay.Height, region.Height);
                this.NeedDisplay = new Rect(x, y, w, h);
            }

            if (this.SuperView != null)
                this.SuperView.ChildNeedsDisplay();
            if (this.subviews == null)
                return;
            foreach (View view in this.subviews)
                if (view.Frame.IntersectsWith(region))
                {
                    Rect childRegion = Rect.Intersect(view.Frame, region);
                    childRegion.X -= view.Frame.X;
                    childRegion.Y -= view.Frame.Y;
                    view.SetNeedsDisplay(childRegion);
                }
        }

        /// <summary>
        ///     Flags this view for requiring the children views to be repainted.
        /// </summary>
        public void ChildNeedsDisplay()
        {
            this.childNeedsDisplay = true;
            if (this.SuperView != null)
                this.SuperView.ChildNeedsDisplay();
        }

        /// <summary>
        ///     Adds a subview to this view.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public virtual void Add(View view)
        {
            if (view == null)
                return;
            if (this.subviews == null)
                this.subviews = new List<View>();
            this.subviews.Add(view);
            view.SuperView = this;
            if (view.CanFocus)
                this.CanFocus = true;
            this.SetNeedsDisplay();
        }

        /// <summary>
        ///     Adds the specified views to the view.
        /// </summary>
        /// <param name="views">Array of one or more views (can be optional parameter).</param>
        public void Add(params View[] views)
        {
            if (views == null)
                return;
            foreach (View view in views)
                this.Add(view);
        }

        /// <summary>
        ///     Removes all the widgets from this container.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public virtual void RemoveAll()
        {
            if (this.subviews == null)
                return;

            while (this.subviews.Count > 0)
                this.Remove(this.subviews[0]);
        }

        /// <summary>
        ///     Removes a widget from this container.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public virtual void Remove(View view)
        {
            if (view == null || this.subviews == null)
                return;

            this.SetNeedsDisplay();
            Rect touched = view.Frame;
            this.subviews.Remove(view);
            view.SuperView = null;

            if (this.subviews.Count < 1)
                this.CanFocus = false;

            foreach (View v in this.subviews)
                if (v.Frame.IntersectsWith(touched))
                    view.SetNeedsDisplay();
        }

        /// <summary>
        ///     Clears the view region with the current color.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This clears the entire region used by this view.
        ///     </para>
        /// </remarks>
        public void Clear()
        {
            int h = this.Frame.Height;
            int w = this.Frame.Width;
            for (var line = 0; line < h; line++)
            {
                this.Move(0, line);
                for (var col = 0; col < w; col++)
                    Driver.AddRune(' ');
            }
        }

        /// <summary>
        ///     Clears the specfied rectangular region with the current color
        /// </summary>
        public void Clear(Rect r)
        {
            int h = r.Height;
            int w = r.Width;
            for (var line = 0; line < h; line++)
            {
                this.Move(0, line);
                for (var col = 0; col < w; col++)
                    Driver.AddRune(' ');
            }
        }

        /// <summary>
        ///     Converts the (col,row) position from the view into a screen (col,row).  The values are clamped to (0..ScreenDim-1)
        /// </summary>
        /// <param name="col">View-based column.</param>
        /// <param name="row">View-based row.</param>
        /// <param name="rcol">Absolute column, display relative.</param>
        /// <param name="rrow">Absolute row, display relative.</param>
        /// <param name="clipped">
        ///     Whether to clip the result of the ViewToScreen method, if set to true, the rcol, rrow values are
        ///     clamped to the screen dimensions.
        /// </param>
        internal void ViewToScreen(int col, int row, out int rcol, out int rrow, bool clipped = true)
        {
            // Computes the real row, col relative to the screen.
            rrow = row + this.frame.Y;
            rcol = col + this.frame.X;
            View ccontainer = this.SuperView;
            while (ccontainer != null)
            {
                rrow += ccontainer.frame.Y;
                rcol += ccontainer.frame.X;
                ccontainer = ccontainer.SuperView;
            }

            // The following ensures that the cursor is always in the screen boundaries.
            if (clipped)
            {
                rrow = Math.Max(0, Math.Min(rrow, Driver.Rows - 1));
                rcol = Math.Max(0, Math.Min(rcol, Driver.Cols - 1));
            }
        }

        /// <summary>
        ///     Converts a point from screen coordinates into the view coordinate space.
        /// </summary>
        /// <returns>The mapped point.</returns>
        /// <param name="x">X screen-coordinate point.</param>
        /// <param name="y">Y screen-coordinate point.</param>
        public Point ScreenToView(int x, int y)
        {
            if (this.SuperView == null)
                return new Point(x - this.Frame.X, y - this.frame.Y);
            Point parent = this.SuperView.ScreenToView(x, y);
            return new Point(parent.X - this.frame.X, parent.Y - this.frame.Y);
        }

        // Converts a rectangle in view coordinates to screen coordinates.
        private Rect RectToScreen(Rect rect)
        {
            this.ViewToScreen(rect.X, rect.Y, out int x, out int y, false);
            return new Rect(x, y, rect.Width, rect.Height);
        }

        // Clips a rectangle in screen coordinates to the dimensions currently available on the screen
        private Rect ScreenClip(Rect rect)
        {
            int x = rect.X < 0 ? 0 : rect.X;
            int y = rect.Y < 0 ? 0 : rect.Y;
            int w = rect.X + rect.Width >= Driver.Cols ? Driver.Cols - rect.X : rect.Width;
            int h = rect.Y + rect.Height >= Driver.Rows ? Driver.Rows - rect.Y : rect.Height;

            return new Rect(x, y, w, h);
        }

        /// <summary>
        ///     Sets the Console driver's clip region to the current View's Bounds.
        /// </summary>
        /// <returns>The existing driver's Clip region, which can be then set by setting the Driver.Clip property.</returns>
        public Rect ClipToBounds()
        {
            return this.SetClip(this.Bounds);
        }

        /// <summary>
        ///     Sets the clipping region to the specified region, the region is view-relative
        /// </summary>
        /// <returns>The previous clip region.</returns>
        /// <param name="rect">Rectangle region to clip into, the region is view-relative.</param>
        public Rect SetClip(Rect rect)
        {
            Rect bscreen = this.RectToScreen(rect);
            Rect previous = Driver.Clip;
            Driver.Clip = this.ScreenClip(this.RectToScreen(this.Bounds));
            return previous;
        }

        /// <summary>
        ///     Draws a frame in the current view, clipped by the boundary of this view
        /// </summary>
        /// <param name="rect">Rectangular region for the frame to be drawn.</param>
        /// <param name="padding">The padding to add to the drawn frame.</param>
        /// <param name="fill">If set to <c>true</c> it fill will the contents.</param>
        public void DrawFrame(Rect rect, int padding = 0, bool fill = false)
        {
            Rect scrRect = this.RectToScreen(rect);
            Rect savedClip = Driver.Clip;
            Driver.Clip = this.ScreenClip(this.RectToScreen(this.Bounds));
            Driver.DrawFrame(scrRect, padding, fill);
            Driver.Clip = savedClip;
        }

        /// <summary>
        ///     Utility function to draw strings that contain a hotkey
        /// </summary>
        /// <param name="text">String to display, the underscoore before a letter flags the next letter as the hotkey.</param>
        /// <param name="hotColor">Hot color.</param>
        /// <param name="normalColor">Normal color.</param>
        public void DrawHotString(ustring text, Attribute hotColor, Attribute normalColor)
        {
            Driver.SetAttribute(normalColor);
            foreach (uint rune in text)
            {
                if (rune == '_')
                {
                    Driver.SetAttribute(hotColor);
                    continue;
                }

                Driver.AddRune(rune);
                Driver.SetAttribute(normalColor);
            }
        }

        /// <summary>
        ///     Utility function to draw strings that contains a hotkey using a colorscheme and the "focused" state.
        /// </summary>
        /// <param name="text">String to display, the underscoore before a letter flags the next letter as the hotkey.</param>
        /// <param name="focused">
        ///     If set to <c>true</c> this uses the focused colors from the color scheme, otherwise the regular
        ///     ones.
        /// </param>
        /// <param name="scheme">The color scheme to use.</param>
        public void DrawHotString(ustring text, bool focused, ColorScheme scheme)
        {
            if (focused)
                this.DrawHotString(text, scheme.HotFocus, scheme.Focus);
            else
                this.DrawHotString(text, scheme.HotNormal, scheme.Normal);
        }

        /// <summary>
        ///     This moves the cursor to the specified column and row in the view.
        /// </summary>
        /// <returns>The move.</returns>
        /// <param name="col">Col.</param>
        /// <param name="row">Row.</param>
        public void Move(int col, int row)
        {
            this.ViewToScreen(col, row, out int rcol, out int rrow);
            Driver.Move(rcol, rrow);
        }

        /// <summary>
        ///     Positions the cursor in the right position based on the currently focused view in the chain.
        /// </summary>
        public virtual void PositionCursor()
        {
            if (this.Focused != null)
                this.Focused.PositionCursor();
            else
                this.Move(this.frame.X, this.frame.Y);
        }

        /// <summary>
        ///     Displays the specified character in the specified column and row.
        /// </summary>
        /// <param name="col">Col.</param>
        /// <param name="row">Row.</param>
        /// <param name="ch">Ch.</param>
        public void AddRune(int col, int row, Rune ch)
        {
            if (row < 0 || col < 0)
                return;
            if (row > this.frame.Height - 1 || col > this.frame.Width - 1)
                return;
            this.Move(col, row);
            Driver.AddRune(ch);
        }

        /// <summary>
        ///     Removes the SetNeedsDisplay and the ChildNeedsDisplay setting on this view.
        /// </summary>
        protected void ClearNeedsDisplay()
        {
            this.NeedDisplay = Rect.Empty;
            this.childNeedsDisplay = false;
        }

        /// <summary>
        ///     Performs a redraw of this view and its subviews, only redraws the views that have been flagged for a re-display.
        /// </summary>
        /// <param name="region">The region to redraw, this is relative to the view itself.</param>
        /// <remarks>
        ///     <para>
        ///         Views should set the color that they want to use on entry, as otherwise this will inherit
        ///         the last color that was set globaly on the driver.
        ///     </para>
        /// </remarks>
        public virtual void Redraw(Rect region)
        {
            var clipRect = new Rect(Point.Empty, this.frame.Size);

            if (this.subviews != null)
                foreach (View view in this.subviews)
                    if (!view.NeedDisplay.IsEmpty || view.childNeedsDisplay)
                    {
                        if (view.Frame.IntersectsWith(clipRect) && view.Frame.IntersectsWith(region))
                            view.Redraw(view.Bounds);
                        view.NeedDisplay = Rect.Empty;
                        view.childNeedsDisplay = false;
                    }

            this.ClearNeedsDisplay();
        }

        /// <summary>
        ///     Focuses the specified sub-view.
        /// </summary>
        /// <param name="view">View.</param>
        public void SetFocus(View view)
        {
            if (view == null)
                return;
            //Console.WriteLine ($"Request to focus {view}");
            if (!view.CanFocus)
                return;
            if (this.Focused == view)
                return;

            // Make sure that this view is a subview
            View c;
            for (c = view.SuperView; c != null; c = c.SuperView)
                if (c == this)
                    break;
            if (c == null)
                throw new ArgumentException("the specified view is not part of the hierarchy of this view");

            if (this.Focused != null)
                this.Focused.HasFocus = false;

            this.Focused = view;
            this.Focused.HasFocus = true;
            this.Focused.EnsureFocus();

            // Send focus upwards
            this.SuperView?.SetFocus(this);
        }

        /// <param name="keyEvent">Contains the details about the key that produced the event.</param>
        public override bool ProcessKey(KeyEvent keyEvent)
        {
            if (this.Focused?.ProcessKey(keyEvent) == true)
                return true;

            return false;
        }

        /// <param name="keyEvent">Contains the details about the key that produced the event.</param>
        public override bool ProcessHotKey(KeyEvent keyEvent)
        {
            if (this.subviews == null || this.subviews.Count == 0)
                return false;
            foreach (View view in this.subviews)
                if (view.ProcessHotKey(keyEvent))
                    return true;
            return false;
        }

        /// <param name="keyEvent">Contains the details about the key that produced the event.</param>
        public override bool ProcessColdKey(KeyEvent keyEvent)
        {
            if (this.subviews == null || this.subviews.Count == 0)
                return false;
            foreach (View view in this.subviews)
                if (view.ProcessColdKey(keyEvent))
                    return true;
            return false;
        }

        /// <summary>
        ///     Finds the first view in the hierarchy that wants to get the focus if nothing is currently focused, otherwise, it
        ///     does nothing.
        /// </summary>
        public void EnsureFocus()
        {
            if (this.Focused == null)
                this.FocusFirst();
        }

        /// <summary>
        ///     Focuses the first focusable subview if one exists.
        /// </summary>
        public void FocusFirst()
        {
            if (this.subviews == null)
            {
                this.SuperView.SetFocus(this);
                return;
            }

            foreach (View view in this.subviews)
                if (view.CanFocus)
                {
                    this.SetFocus(view);
                    return;
                }
        }

        /// <summary>
        ///     Focuses the last focusable subview if one exists.
        /// </summary>
        public void FocusLast()
        {
            if (this.subviews == null)
                return;

            for (int i = this.subviews.Count; i > 0;)
            {
                i--;

                View v = this.subviews[i];
                if (v.CanFocus)
                {
                    this.SetFocus(v);
                    return;
                }
            }
        }

        /// <summary>
        ///     Focuses the previous view.
        /// </summary>
        /// <returns><c>true</c>, if previous was focused, <c>false</c> otherwise.</returns>
        public bool FocusPrev()
        {
            if (this.subviews == null || this.subviews.Count == 0)
                return false;

            if (this.Focused == null)
            {
                this.FocusLast();
                return true;
            }

            int focused_idx = -1;
            for (int i = this.subviews.Count; i > 0;)
            {
                i--;
                View w = this.subviews[i];

                if (w.HasFocus)
                {
                    if (w.FocusPrev())
                        return true;
                    focused_idx = i;
                    continue;
                }

                if (w.CanFocus && focused_idx != -1)
                {
                    this.Focused.HasFocus = false;

                    if (w.CanFocus)
                        w.FocusLast();

                    this.SetFocus(w);
                    return true;
                }
            }

            if (focused_idx != -1)
            {
                this.FocusLast();
                return true;
            }

            if (this.Focused != null)
            {
                this.Focused.HasFocus = false;
                this.Focused = null;
            }

            return false;
        }

        /// <summary>
        ///     Focuses the next view.
        /// </summary>
        /// <returns><c>true</c>, if next was focused, <c>false</c> otherwise.</returns>
        public bool FocusNext()
        {
            if (this.subviews == null || this.subviews.Count == 0)
                return false;

            if (this.Focused == null)
            {
                this.FocusFirst();
                return this.Focused != null;
            }

            int n = this.subviews.Count;
            int focused_idx = -1;
            for (var i = 0; i < n; i++)
            {
                View w = this.subviews[i];

                if (w.HasFocus)
                {
                    if (w.FocusNext())
                        return true;
                    focused_idx = i;
                    continue;
                }

                if (w.CanFocus && focused_idx != -1)
                {
                    this.Focused.HasFocus = false;

                    if (w != null && w.CanFocus)
                        w.FocusFirst();

                    this.SetFocus(w);
                    return true;
                }
            }

            if (this.Focused != null)
            {
                this.Focused.HasFocus = false;
                this.Focused = null;
            }

            return false;
        }

        /// <summary>
        ///     Computes the RelativeLayout for the view, given the frame for its container.
        /// </summary>
        /// <param name="hostFrame">The Frame for the host.</param>
        internal void RelativeLayout(Rect hostFrame)
        {
            int w, h, _x, _y;

            if (this.x is Pos.PosCenter)
            {
                if (this.width == null)
                    w = hostFrame.Width;
                else
                    w = this.width.Anchor(hostFrame.Width);
                _x = this.x.Anchor(hostFrame.Width - w);
            }
            else
            {
                if (this.x == null)
                    _x = 0;
                else
                    _x = this.x.Anchor(hostFrame.Width);
                if (this.width == null)
                    w = hostFrame.Width;
                else
                    w = this.width.Anchor(hostFrame.Width - _x);
            }

            if (this.y is Pos.PosCenter)
            {
                if (this.height == null)
                    h = hostFrame.Height;
                else
                    h = this.height.Anchor(hostFrame.Height);
                _y = this.y.Anchor(hostFrame.Height - h);
            }
            else
            {
                if (this.y == null)
                    _y = 0;
                else
                    _y = this.y.Anchor(hostFrame.Height);
                if (this.height == null)
                    h = hostFrame.Height;
                else
                    h = this.height.Anchor(hostFrame.Height - _y);
            }

            this.Frame = new Rect(_x, _y, w, h);
        }

        // https://en.wikipedia.org/wiki/Topological_sorting
        private static List<View> TopologicalSort(HashSet<View> nodes, HashSet<(View, View)> edges)
        {
            var result = new List<View>();

            // Set of all nodes with no incoming edges
            var S = new HashSet<View>(nodes.Where(n => edges.All(e => e.Item2.Equals(n) == false)));

            while (S.Any())
            {
                //  remove a node n from S
                View n = S.First();
                S.Remove(n);

                // add n to tail of L
                result.Add(n);

                // for each node m with an edge e from n to m do
                foreach ((View, View) e in edges.Where(e => e.Item1.Equals(n)).ToList())
                {
                    View m = e.Item2;

                    // remove edge e from the graph
                    edges.Remove(e);

                    // if m has no other incoming edges then
                    if (edges.All(me => me.Item2.Equals(m) == false))
                        S.Add(m);
                }
            }

            // if graph has edges then
            if (edges.Any())
                return null;
            return result;
        }

        /// <summary>
        ///     This virtual method is invoked when a view starts executing or
        ///     when the dimensions of the view have changed, for example in
        ///     response to the container view or terminal resizing.
        /// </summary>
        public virtual void LayoutSubviews()
        {
            if (!this.layoutNeeded)
                return;

            // Sort out the dependencies of the X, Y, Width, Height properties
            var nodes = new HashSet<View>();
            var edges = new HashSet<(View, View)>();

            foreach (View v in this.Subviews)
            {
                nodes.Add(v);
                if (v.LayoutStyle == LayoutStyle.Computed)
                {
                    if (v.X is Pos.PosView)
                        edges.Add((v, (v.X as Pos.PosView).Target));
                    if (v.Y is Pos.PosView)
                        edges.Add((v, (v.Y as Pos.PosView).Target));
                    if (v.Width is Dim.DimView)
                        edges.Add((v, (v.Width as Dim.DimView).Target));
                    if (v.Height is Dim.DimView)
                        edges.Add((v, (v.Height as Dim.DimView).Target));
                }
            }

            List<View> ordered = TopologicalSort(nodes, edges);
            ordered.Reverse();
            if (ordered == null)
                throw new Exception("There is a recursive cycle in the relative Pos/Dim in the views of " + this);

            foreach (View v in ordered)
            {
                if (v.LayoutStyle == LayoutStyle.Computed)
                    v.RelativeLayout(this.Frame);

                v.LayoutSubviews();
                v.layoutNeeded = false;
            }

            this.layoutNeeded = false;
        }

        /// <summary>
        ///     Returns a <see cref="T:System.String" /> that represents the current <see cref="T:Terminal.Gui.View" />.
        /// </summary>
        /// <returns>A <see cref="T:System.String" /> that represents the current <see cref="T:Terminal.Gui.View" />.</returns>
        public override string ToString()
        {
            return $"{this.GetType().Name}({this.Id})({this.Frame})";
        }
    }

    /// <summary>
    ///     Toplevel views can be modally executed.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Toplevels can be modally executing views, and they return control
    ///         to the caller when the "Running" property is set to false, or
    ///         by calling <see cref="M:Terminal.Gui.Application.RequestStop()" />
    ///     </para>
    ///     <para>
    ///         There will be a toplevel created for you on the first time use
    ///         and can be accessed from the property <see cref="P:Terminal.Gui.Application.Top" />,
    ///         but new toplevels can be created and ran on top of it.   To run, create the
    ///         toplevel and then invoke <see cref="M:Terminal.Gui.Application.Run" /> with the
    ///         new toplevel.
    ///     </para>
    /// </remarks>
    public class Toplevel : View
    {
        /// <summary>
        ///     This flag is checked on each iteration of the mainloop and it continues
        ///     running until this flag is set to false.
        /// </summary>
        public bool Running;

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:Terminal.Gui.Toplevel" /> class with the specified absolute layout.
        /// </summary>
        /// <param name="frame">Frame.</param>
        public Toplevel(Rect frame) : base(frame)
        {
            this.ColorScheme = Colors.Base;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:Terminal.Gui.Toplevel" /> class with Computed layout, defaulting to
        ///     <see langword="async" /> full screen.
        /// </summary>
        public Toplevel()
        {
            this.ColorScheme = Colors.Base;
            this.Width = Dim.Fill();
            this.Height = Dim.Fill();
        }

        public override bool CanFocus => true;

        /// <summary>
        ///     Convenience factory method that creates a new toplevel with the current terminal dimensions.
        /// </summary>
        /// <returns>The create.</returns>
        public static Toplevel Create()
        {
            return new Toplevel(new Rect(0, 0, Driver.Cols, Driver.Rows));
        }

        public override bool ProcessKey(KeyEvent keyEvent)
        {
            if (base.ProcessKey(keyEvent))
                return true;

            switch (keyEvent.Key)
            {
                case Key.ControlC:
                    // TODO: stop current execution of this container
                    break;
                case Key.ControlZ:
                    Driver.Suspend();
                    return true;

#if false
			case Key.F5:
				Application.DebugDrawBounds = !Application.DebugDrawBounds;
				SetNeedsDisplay ();
				return true;
#endif
                case Key.Tab:
                case Key.CursorRight:
                case Key.CursorDown:
                    View old = this.Focused;
                    if (!this.FocusNext())
                        this.FocusNext();
                    if (old != this.Focused)
                    {
                        old?.SetNeedsDisplay();
                        this.Focused?.SetNeedsDisplay();
                    }

                    return true;
                case Key.CursorLeft:
                case Key.CursorUp:
                case Key.BackTab:
                    old = this.Focused;
                    if (!this.FocusPrev())
                        this.FocusPrev();
                    if (old != this.Focused)
                    {
                        old?.SetNeedsDisplay();
                        this.Focused?.SetNeedsDisplay();
                    }

                    return true;

                case Key.ControlL:
                    Application.Refresh();
                    return true;
            }

            return false;
        }

        /// <summary>
        ///     This method is invoked by Application.Begin as part of the Application.Run after
        ///     the views have been laid out, and before the views are drawn for the first time.
        /// </summary>
        public virtual void WillPresent()
        {
            this.FocusFirst();
        }
    }

    /// <summary>
    ///     A toplevel view that draws a frame around its region and has a "ContentView" subview where the contents are added.
    /// </summary>
    public class Window : Toplevel, IEnumerable
    {
        private readonly View contentView;

        private readonly int padding;

        private ustring title;

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:Terminal.Gui.Gui.Window" /> class with an optional title and a set
        ///     frame.
        /// </summary>
        /// <param name="frame">Frame.</param>
        /// <param name="title">Title.</param>
        public Window(Rect frame, ustring title = null) : this(frame, title, 0)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:Terminal.Gui.Window" /> class with an optional title.
        /// </summary>
        /// <param name="title">Title.</param>
        public Window(ustring title = null) : this(title, 0)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:Terminal.Gui.Window" /> with
        ///     the specified frame for its location, with the specified border
        ///     an optional title.
        /// </summary>
        /// <param name="frame">Frame.</param>
        /// <param name="padding">Number of characters to use for padding of the drawn frame.</param>
        /// <param name="title">Title.</param>
        public Window(Rect frame, ustring title = null, int padding = 0) : base(frame)
        {
            this.Title = title;
            int wb = 2 * (1 + padding);
            this.padding = padding;
            var cFrame = new Rect(1 + padding, 1 + padding, frame.Width - wb, frame.Height - wb);
            this.contentView = new ContentView(cFrame);
            base.Add(this.contentView);
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:Terminal.Gui.Window" /> with
        ///     the specified frame for its location, with the specified border
        ///     an optional title.
        /// </summary>
        /// <param name="padding">Number of characters to use for padding of the drawn frame.</param>
        /// <param name="title">Title.</param>
        public Window(ustring title = null, int padding = 0)
        {
            this.Title = title;
            int wb = 1 + padding;
            this.padding = padding;
            this.contentView = new ContentView
            {
                X = wb,
                Y = wb,
                Width = Dim.Fill(wb),
                Height = Dim.Fill(wb)
            };
            base.Add(this.contentView);
        }

        /// <summary>
        ///     The title to be displayed for this window.
        /// </summary>
        /// <value>The title.</value>
        public ustring Title
        {
            get => this.title;
            set
            {
                this.title = value;
                this.SetNeedsDisplay();
            }
        }

        /// <summary>
        ///     Enumerates the various views in the ContentView.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public new IEnumerator GetEnumerator()
        {
            return this.contentView.GetEnumerator();
        }

        private void DrawFrame()
        {
            this.DrawFrame(new Rect(0, 0, this.Frame.Width, this.Frame.Height), this.padding, true);
        }

        /// <summary>
        ///     Add the specified view to the ContentView.
        /// </summary>
        /// <param name="view">View to add to the window.</param>
        public override void Add(View view)
        {
            this.contentView.Add(view);
            if (view.CanFocus)
                this.CanFocus = true;
        }


        /// <summary>
        ///     Removes a widget from this container.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public override void Remove(View view)
        {
            if (view == null)
                return;

            this.SetNeedsDisplay();
            Rect touched = view.Frame;
            this.contentView.Remove(view);

            if (this.contentView.Subviews.Count < 1)
                this.CanFocus = false;
        }

        public override void Redraw(Rect bounds)
        {
            if (!this.NeedDisplay.IsEmpty)
            {
                Driver.SetAttribute(this.ColorScheme.Normal);
                this.DrawFrame();
                if (this.HasFocus)
                    Driver.SetAttribute(this.ColorScheme.Normal);
                int width = this.Frame.Width;
                if (this.Title != null && width > 4)
                {
                    this.Move(1 + this.padding, this.padding);
                    Driver.AddRune(' ');
                    ustring str = this.Title.Length > width ? this.Title[0, width - 4] : this.Title;
                    Driver.AddStr(str);
                    Driver.AddRune(' ');
                }

                Driver.SetAttribute(this.ColorScheme.Normal);
            }

            this.contentView.Redraw(this.contentView.Bounds);
            this.ClearNeedsDisplay();
        }

        private class ContentView : View
        {
            public ContentView(Rect frame) : base(frame)
            {
            }

            public ContentView()
            {
            }
#if false
			public override void Redraw (Rect region)
			{
				Driver.SetAttribute (ColorScheme.Focus);

				for (int y = 0; y < Frame.Height; y++) {
					Move (0, y);
					for (int x = 0; x < Frame.Width; x++) {

						Driver.AddRune ('x');
					}
				}
			}
#endif
        }

#if true
        // 
        // It does not look like the event is raised on clicked-drag
        // need to figure that out.
        //
        private Point? dragPosition;

        public override bool MouseEvent(MouseEvent mouseEvent)
        {
            // The code is currently disabled, because the 
            // Driver.UncookMouse does not seem to have an effect if there is 
            // a pending mouse event activated.
            if (true)
                return false;

            if (mouseEvent.Flags == MouseFlags.Button1Pressed || mouseEvent.Flags == MouseFlags.Button4Pressed)
            {
                if (this.dragPosition.HasValue)
                {
                    int dx = mouseEvent.X - this.dragPosition.Value.X;
                    int dy = mouseEvent.Y - this.dragPosition.Value.Y;

                    int nx = this.Frame.X + dx;
                    int ny = this.Frame.Y + dy;
                    if (nx < 0)
                        nx = 0;
                    if (ny < 0)
                        ny = 0;

                    //Demo.ml2.Text = $"{dx},{dy}";
                    this.dragPosition = new Point(mouseEvent.X, mouseEvent.Y);

                    // TODO: optimize, only SetNeedsDisplay on the before/after regions.
                    if (this.SuperView == null)
                        Application.Refresh();
                    this.SuperView.SetNeedsDisplay();
                    this.Frame = new Rect(nx, ny, this.Frame.Width, this.Frame.Height);
                    this.SetNeedsDisplay();
                    return true;
                }

                // Only start grabbing if the user clicks on the title bar.
                if (mouseEvent.Y == 0)
                {
                    this.dragPosition = new Point(mouseEvent.X, mouseEvent.Y);
                    Application.GrabMouse(this);
                }

                //Demo.ml2.Text = $"Starting at {dragPosition}";
                return true;
            }

            if (mouseEvent.Flags == MouseFlags.Button1Released)
            {
                Application.UngrabMouse();
                Driver.UncookMouse();

                this.dragPosition = null;
                //Driver.StopReportingMouseMoves ();
            }

            //Demo.ml.Text = me.ToString ();
            return false;
        }
#endif
    }

    /// <summary>
    ///     The application driver for gui.cs
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         You can hook up to the Iteration event to have your method
    ///         invoked on each iteration of the mainloop.
    ///     </para>
    ///     <para>
    ///         Creates a mainloop to process input events, handle timers and
    ///         other sources of data.   It is accessible via the MainLoop property.
    ///     </para>
    ///     <para>
    ///         When invoked sets the SynchronizationContext to one that is tied
    ///         to the mainloop, allowing user code to use async/await.
    ///     </para>
    /// </remarks>
    public static class Application
    {
        /// <summary>
        ///     The current Console Driver in use.
        /// </summary>
        public static ConsoleDriver Driver;

        private static readonly Stack<Toplevel> toplevels = new Stack<Toplevel>();

        /// <summary>
        ///     If set, it forces the use of the System.Console-based driver.
        /// </summary>
        public static bool UseSystemConsole;

        private static View mouseGrabView;

        /// <summary>
        ///     Merely a debugging aid to see the raw mouse events
        /// </summary>
        public static Action<MouseEvent> RootMouseEvent;

        internal static bool DebugDrawBounds;

        /// <summary>
        ///     The Toplevel object used for the application on startup.
        /// </summary>
        /// <value>The top.</value>
        public static Toplevel Top { get; private set; }

        /// <summary>
        ///     The current toplevel object.   This is updated when Application.Run enters and leaves and points to the current
        ///     toplevel.
        /// </summary>
        /// <value>The current.</value>
        public static Toplevel Current { get; private set; }

        /// <summary>
        ///     The mainloop driver for the applicaiton
        /// </summary>
        /// <value>The main loop.</value>
        public static MainLoop MainLoop { get; private set; }

        /// <summary>
        ///     This event is raised on each iteration of the
        ///     main loop.
        /// </summary>
        /// <remarks>
        ///     See also <see cref="Timeout" />
        /// </remarks>
        public static event EventHandler Iteration;

        /// <summary>
        ///     Returns a rectangle that is centered in the screen for the provided size.
        /// </summary>
        /// <returns>The centered rect.</returns>
        /// <param name="size">Size for the rectangle.</param>
        public static Rect MakeCenteredRect(Size size)
        {
            return new Rect(new Point((Driver.Cols - size.Width) / 2, (Driver.Rows - size.Height) / 2), size);
        }

        /// <summary>
        ///     Initializes the Application
        /// </summary>
        public static void Init()
        {
            if (Top != null)
                return;

            PlatformID p = Environment.OSVersion.Platform;
            IMainLoopDriver mainLoopDriver;

            if (UseSystemConsole)
            {
                mainLoopDriver = new NetMainLoop();
                Driver = new NetDriver();
            }
            else if (p == PlatformID.Win32NT || p == PlatformID.Win32S || p == PlatformID.Win32Windows)
            {
                var windowsDriver = new WindowsDriver();
                mainLoopDriver = windowsDriver;
                Driver = windowsDriver;
            }
            else
            {
                mainLoopDriver = new UnixMainLoop();
                Driver = new CursesDriver();
            }

            Driver.Init(TerminalResized);
            MainLoop = new MainLoop(mainLoopDriver);
            SynchronizationContext.SetSynchronizationContext(new MainLoopSyncContext(MainLoop));
            Top = Toplevel.Create();
            Current = Top;
        }

        private static void ProcessKeyEvent(KeyEvent ke)
        {
            if (Current.ProcessHotKey(ke))
                return;

            if (Current.ProcessKey(ke))
                return;

            // Process the key normally
            if (Current.ProcessColdKey(ke))
                return;
        }

        private static View FindDeepestView(View start, int x, int y, out int resx, out int resy)
        {
            Rect startFrame = start.Frame;

            if (!startFrame.Contains(x, y))
            {
                resx = 0;
                resy = 0;
                return null;
            }

            if (start.Subviews != null)
            {
                int count = start.Subviews.Count;
                if (count > 0)
                {
                    int rx = x - startFrame.X;
                    int ry = y - startFrame.Y;
                    for (int i = count - 1; i >= 0; i--)
                    {
                        View v = start.Subviews[i];
                        if (v.Frame.Contains(rx, ry))
                        {
                            View deep = FindDeepestView(v, rx, ry, out resx, out resy);
                            if (deep == null)
                                return v;
                            return deep;
                        }
                    }
                }
            }

            resx = x - startFrame.X;
            resy = y - startFrame.Y;
            return start;
        }

        /// <summary>
        ///     Grabs the mouse, forcing all mouse events to be routed to the specified view until UngrabMouse is called.
        /// </summary>
        /// <returns>The grab.</returns>
        /// <param name="view">View that will receive all mouse events until UngrabMouse is invoked.</param>
        public static void GrabMouse(View view)
        {
            if (view == null)
                return;
            mouseGrabView = view;
            Driver.UncookMouse();
        }

        /// <summary>
        ///     Releases the mouse grab, so mouse events will be routed to the view on which the mouse is.
        /// </summary>
        public static void UngrabMouse()
        {
            mouseGrabView = null;
            Driver.CookMouse();
        }

        private static void ProcessMouseEvent(MouseEvent me)
        {
            RootMouseEvent?.Invoke(me);
            if (mouseGrabView != null)
            {
                Point newxy = mouseGrabView.ScreenToView(me.X, me.Y);
                var nme = new MouseEvent
                {
                    X = newxy.X,
                    Y = newxy.Y,
                    Flags = me.Flags
                };
                mouseGrabView.MouseEvent(me);
                return;
            }

            int rx, ry;
            View view = FindDeepestView(Current, me.X, me.Y, out rx, out ry);
            if (view != null)
            {
                if (!view.WantMousePositionReports && me.Flags == MouseFlags.ReportMousePosition)
                    return;

                var nme = new MouseEvent
                {
                    X = rx,
                    Y = ry,
                    Flags = me.Flags
                };
                // Should we bubbled up the event, if it is not handled?
                view.MouseEvent(nme);
            }
        }

        /// <summary>
        ///     Building block API: Prepares the provided toplevel for execution.
        /// </summary>
        /// <returns>The runstate handle that needs to be passed to the End() method upon completion.</returns>
        /// <param name="toplevel">Toplevel to prepare execution for.</param>
        /// <remarks>
        ///     This method prepares the provided toplevel for running with the focus,
        ///     it adds this to the list of toplevels, sets up the mainloop to process the
        ///     event, lays out the subviews, focuses the first element, and draws the
        ///     toplevel in the screen.   This is usually followed by executing
        ///     the <see cref="RunLoop" /> method, and then the <see cref="End(RunState)" /> method upon termination which will
        ///     undo these changes.
        /// </remarks>
        public static RunState Begin(Toplevel toplevel)
        {
            if (toplevel == null)
                throw new ArgumentNullException(nameof(toplevel));
            var rs = new RunState(toplevel);

            Init();
            toplevels.Push(toplevel);
            Current = toplevel;
            Driver.PrepareToRun(MainLoop, ProcessKeyEvent, ProcessMouseEvent);
            if (toplevel.LayoutStyle == LayoutStyle.Computed)
                toplevel.RelativeLayout(new Rect(0, 0, Driver.Cols, Driver.Rows));
            toplevel.LayoutSubviews();
            toplevel.WillPresent();
            Redraw(toplevel);
            toplevel.PositionCursor();
            Driver.Refresh();

            return rs;
        }

        /// <summary>
        ///     Building block API: completes the exection of a Toplevel that was started with Begin.
        /// </summary>
        /// <param name="runState">The runstate returned by the <see cref="Begin(Toplevel)" /> method.</param>
        public static void End(RunState runState)
        {
            if (runState == null)
                throw new ArgumentNullException(nameof(runState));

            runState.Dispose();
        }

        private static void Shutdown()
        {
            Driver.End();
        }

        private static void Redraw(View view)
        {
            view.Redraw(view.Bounds);
            Driver.Refresh();
        }

        private static void Refresh(View view)
        {
            view.Redraw(view.Bounds);
            Driver.Refresh();
        }

        /// <summary>
        ///     Triggers a refresh of the entire display.
        /// </summary>
        public static void Refresh()
        {
            Driver.UpdateScreen();
            View last = null;
            foreach (Toplevel v in toplevels.Reverse())
            {
                v.SetNeedsDisplay();
                v.Redraw(v.Bounds);
                last = v;
            }

            last?.PositionCursor();
            Driver.Refresh();
        }

        internal static void End(View view)
        {
            if (toplevels.Peek() != view)
                throw new ArgumentException("The view that you end with must be balanced");
            toplevels.Pop();
            if (toplevels.Count == 0)
            {
                Shutdown();
            }
            else
            {
                Current = toplevels.Peek();
                Refresh();
            }
        }

        /// <summary>
        ///     Building block API: Runs the main loop for the created dialog
        /// </summary>
        /// <remarks>
        ///     Use the wait parameter to control whether this is a
        ///     blocking or non-blocking call.
        /// </remarks>
        /// <param name="state">The state returned by the Begin method.</param>
        /// <param name="wait">
        ///     By default this is true which will execute the runloop waiting for events, if you pass false, you
        ///     can use this method to run a single iteration of the events.
        /// </param>
        public static void RunLoop(RunState state, bool wait = true)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (state.Toplevel == null)
                throw new ObjectDisposedException("state");

            for (state.Toplevel.Running = true; state.Toplevel.Running;)
            {
                if (MainLoop.EventsPending(wait))
                {
                    MainLoop.MainIteration();
                    if (Iteration != null)
                        Iteration(null, EventArgs.Empty);
                }
                else if (wait == false)
                {
                    return;
                }

                if (!state.Toplevel.NeedDisplay.IsEmpty || state.Toplevel.childNeedsDisplay)
                {
                    state.Toplevel.Redraw(state.Toplevel.Bounds);
                    if (DebugDrawBounds)
                        DrawBounds(state.Toplevel);
                    state.Toplevel.PositionCursor();
                    Driver.Refresh();
                }
                else
                {
                    Driver.UpdateCursor();
                }
            }
        }

        // Need to look into why this does not work properly.
        private static void DrawBounds(View v)
        {
            v.DrawFrame(v.Frame, 0, false);
            if (v.Subviews != null && v.Subviews.Count > 0)
                foreach (View sub in v.Subviews)
                    DrawBounds(sub);
        }

        /// <summary>
        ///     Runs the application with the built-in toplevel view
        /// </summary>
        public static void Run()
        {
            Run(Top);
        }

        /// <summary>
        ///     Runs the main loop on the given container.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This method is used to start processing events
        ///         for the main application, but it is also used to
        ///         run modal dialog boxes.
        ///     </para>
        ///     <para>
        ///         To make a toplevel stop execution, set the "Running"
        ///         property to false.
        ///     </para>
        ///     <para>
        ///         This is equivalent to calling Begin on the toplevel view, followed by RunLoop with the
        ///         returned value, and then calling end on the return value.
        ///     </para>
        ///     <para>
        ///         Alternatively, if your program needs to control the main loop and needs to
        ///         process events manually, you can invoke Begin to set things up manually and then
        ///         repeatedly call RunLoop with the wait parameter set to false.   By doing this
        ///         the RunLoop method will only process any pending events, timers, idle handlers and
        ///         then return control immediately.
        ///     </para>
        /// </remarks>
        public static void Run(Toplevel view)
        {
            RunState runToken = Begin(view);
            RunLoop(runToken);
            End(runToken);
        }

        /// <summary>
        ///     Stops running the most recent toplevel
        /// </summary>
        public static void RequestStop()
        {
            Current.Running = false;
        }

        private static void TerminalResized()
        {
            var full = new Rect(0, 0, Driver.Cols, Driver.Rows);
            Driver.Clip = full;
            foreach (Toplevel t in toplevels)
            {
                t.RelativeLayout(full);
                t.LayoutSubviews();
            }

            Refresh();
        }

        //
        // provides the sync context set while executing code in gui.cs, to let
        // users use async/await on their code
        //
        private class MainLoopSyncContext : SynchronizationContext
        {
            private readonly MainLoop mainLoop;

            public MainLoopSyncContext(MainLoop mainLoop)
            {
                this.mainLoop = mainLoop;
            }

            public override SynchronizationContext CreateCopy()
            {
                return new MainLoopSyncContext(MainLoop);
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                this.mainLoop.AddIdle(() =>
                {
                    d(state);
                    return false;
                });
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                this.mainLoop.Invoke(() => { d(state); });
            }
        }

        /// <summary>
        ///     Captures the execution state for the provided TopLevel view.
        /// </summary>
        public class RunState : IDisposable
        {
            internal Toplevel Toplevel;

            internal RunState(Toplevel view)
            {
                this.Toplevel = view;
            }

            /// <summary>
            ///     Releases alTop = l resource used by the <see cref="T:Terminal.Gui.Application.RunState" /> object.
            /// </summary>
            /// <remarks>
            ///     Call <see cref="Dispose()" /> when you are finished using the <see cref="T:Terminal.Gui.Application.RunState" />.
            ///     The
            ///     <see cref="Dispose()" /> method leaves the <see cref="T:Terminal.Gui.Application.RunState" /> in an unusable state.
            ///     After
            ///     calling <see cref="Dispose()" />, you must release all references to the
            ///     <see cref="T:Terminal.Gui.Application.RunState" /> so the garbage collector can reclaim the memory that the
            ///     <see cref="T:Terminal.Gui.Application.RunState" /> was occupying.
            /// </remarks>
            public void Dispose()
            {
                this.Dispose(true);
                GC.SuppressFinalize(this);
            }

            /// <summary>
            ///     Dispose the specified disposing.
            /// </summary>
            /// <returns>The dispose.</returns>
            /// <param name="disposing">If set to <c>true</c> disposing.</param>
            protected virtual void Dispose(bool disposing)
            {
                if (this.Toplevel != null)
                {
                    End(this.Toplevel);
                    this.Toplevel = null;
                }
            }
        }
    }
}