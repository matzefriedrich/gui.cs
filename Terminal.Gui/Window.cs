namespace Terminal.Gui
{
    using System.Collections;

    using NStack;

    using Terminal.Gui.Types;

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
}