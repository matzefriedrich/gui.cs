//
// ScrollView.cs: ScrollView and ScrollBarView views.
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
//
// TODO:
// - Mouse handling in scrollbarview
// - focus in scrollview
// - keyboard handling in scrollview to scroll
// - focus handling in scrollview to auto scroll to focused view
// - Raise events
// - Perhaps allow an option to not display the scrollbar arrow indicators?

namespace Terminal.Gui
{
    using System;

    /// <summary>
    ///     Scrollviews are views that present a window into a virtual space where children views are added.  Similar to the
    ///     iOS UIScrollView.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The subviews that are added to this scrollview are offset by the
    ///         ContentOffset property.   The view itself is a window into the
    ///         space represented by the ContentSize.
    ///     </para>
    ///     <para>
    ///     </para>
    /// </remarks>
    public class ScrollView : View
    {
        private readonly View contentView;

        private readonly ScrollBarView horizontal;

        private readonly ScrollBarView vertical;

        private Point contentOffset;

        private Size contentSize;

        private bool showHorizontalScrollIndicator;

        private bool showVerticalScrollIndicator;

        public ScrollView(Rect frame) : base(frame)
        {
            this.contentView = new View(frame);
            this.vertical = new ScrollBarView(new Rect(frame.Width - 1, 0, 1, frame.Height), frame.Height, 0, true);
            this.vertical.ChangedPosition += delegate { this.ContentOffset = new Point(this.ContentOffset.X, this.vertical.Position); };
            this.horizontal = new ScrollBarView(new Rect(0, frame.Height - 1, frame.Width - 1, 1), frame.Width - 1, 0, false);
            this.horizontal.ChangedPosition += delegate { this.ContentOffset = new Point(this.horizontal.Position, this.ContentOffset.Y); };
            base.Add(this.contentView);
            this.CanFocus = true;
        }

        /// <summary>
        ///     Represents the contents of the data shown inside the scrolview
        /// </summary>
        /// <value>The size of the content.</value>
        public Size ContentSize
        {
            get => this.contentSize;
            set
            {
                this.contentSize = value;
                this.contentView.Frame = new Rect(this.contentOffset, value);
                this.vertical.Size = this.contentSize.Height;
                this.horizontal.Size = this.contentSize.Width;
                this.SetNeedsDisplay();
            }
        }

        /// <summary>
        ///     Represents the top left corner coordinate that is displayed by the scrollview
        /// </summary>
        /// <value>The content offset.</value>
        public Point ContentOffset
        {
            get => this.contentOffset;
            set
            {
                this.contentOffset = new Point(-Math.Abs(value.X), -Math.Abs(value.Y));
                this.contentView.Frame = new Rect(this.contentOffset, this.contentSize);
                this.vertical.Position = Math.Max(0, -this.contentOffset.Y);
                this.horizontal.Position = Math.Max(0, -this.contentOffset.X);
                this.SetNeedsDisplay();
            }
        }

        /// <summary>
        ///     Gets or sets the visibility for the horizontal scroll indicator.
        /// </summary>
        /// <value><c>true</c> if show vertical scroll indicator; otherwise, <c>false</c>.</value>
        public bool ShowHorizontalScrollIndicator
        {
            get => this.showHorizontalScrollIndicator;
            set
            {
                if (value == this.showHorizontalScrollIndicator)
                    return;

                this.showHorizontalScrollIndicator = value;
                this.SetNeedsDisplay();
                if (value)
                    base.Add(this.horizontal);
                else
                    this.Remove(this.horizontal);
            }
        }


        /// <summary>
        ///     /// Gets or sets the visibility for the vertical scroll indicator.
        /// </summary>
        /// <value><c>true</c> if show vertical scroll indicator; otherwise, <c>false</c>.</value>
        public bool ShowVerticalScrollIndicator
        {
            get => this.showVerticalScrollIndicator;
            set
            {
                if (value == this.showVerticalScrollIndicator)
                    return;

                this.showVerticalScrollIndicator = value;
                this.SetNeedsDisplay();
                if (value)
                    base.Add(this.vertical);
                else
                    this.Remove(this.vertical);
            }
        }

        /// <summary>
        ///     Adds the view to the scrollview.
        /// </summary>
        /// <param name="view">The view to add to the scrollview.</param>
        public override void Add(View view)
        {
            this.contentView.Add(view);
        }

        /// <summary>
        ///     This event is raised when the contents have scrolled
        /// </summary>
        public event Action<ScrollView> Scrolled;

        public override void Redraw(Rect region)
        {
            Rect oldClip = this.ClipToBounds();
            Driver.SetAttribute(this.ColorScheme.Normal);
            this.Clear();
            base.Redraw(region);
            Driver.Clip = oldClip;
            Driver.SetAttribute(this.ColorScheme.Normal);
        }

        public override void PositionCursor()
        {
            if (this.Subviews.Count == 0)
                Driver.Move(0, 0);
            else
                base.PositionCursor();
        }

        /// <summary>
        ///     Scrolls the view up.
        /// </summary>
        /// <returns><c>true</c>, if left was scrolled, <c>false</c> otherwise.</returns>
        /// <param name="lines">Number of lines to scroll.</param>
        public bool ScrollUp(int lines)
        {
            if (this.contentOffset.Y < 0)
            {
                this.ContentOffset = new Point(this.contentOffset.X, Math.Min(this.contentOffset.Y + lines, 0));
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Scrolls the view to the left
        /// </summary>
        /// <returns><c>true</c>, if left was scrolled, <c>false</c> otherwise.</returns>
        /// <param name="cols">Number of columns to scroll by.</param>
        public bool ScrollLeft(int cols)
        {
            if (this.contentOffset.X < 0)
            {
                this.ContentOffset = new Point(Math.Min(this.contentOffset.X + cols, 0), this.contentOffset.Y);
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Scrolls the view down.
        /// </summary>
        /// <returns><c>true</c>, if left was scrolled, <c>false</c> otherwise.</returns>
        /// <param name="lines">Number of lines to scroll.</param>
        public bool ScrollDown(int lines)
        {
            int ny = Math.Max(-this.contentSize.Height, this.contentOffset.Y - lines);
            if (ny == this.contentOffset.Y)
                return false;
            this.ContentOffset = new Point(this.contentOffset.X, ny);
            return true;
        }

        /// <summary>
        ///     Scrolls the view to the right.
        /// </summary>
        /// <returns><c>true</c>, if right was scrolled, <c>false</c> otherwise.</returns>
        /// <param name="cols">Number of columns to scroll by.</param>
        public bool ScrollRight(int cols)
        {
            int nx = Math.Max(-this.contentSize.Width, this.contentOffset.X - cols);
            if (nx == this.contentOffset.X)
                return false;

            this.ContentOffset = new Point(nx, this.contentOffset.Y);
            return true;
        }

        public override bool ProcessKey(KeyEvent kb)
        {
            if (base.ProcessKey(kb))
                return true;

            switch (kb.Key)
            {
                case Key.CursorUp:
                    return this.ScrollUp(1);
                case (Key) 'v' | Key.AltMask:
                case Key.PageUp:
                    return this.ScrollUp(this.Bounds.Height);

                case Key.ControlV:
                case Key.PageDown:
                    return this.ScrollDown(this.Bounds.Height);

                case Key.CursorDown:
                    return this.ScrollDown(1);

                case Key.CursorLeft:
                    return this.ScrollLeft(1);

                case Key.CursorRight:
                    return this.ScrollRight(1);
            }

            return false;
        }
    }
}