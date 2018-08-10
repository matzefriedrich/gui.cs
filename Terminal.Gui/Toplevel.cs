namespace Terminal.Gui
{
    using Terminal.Gui.Drivers;
    using Terminal.Gui.Types;

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
}