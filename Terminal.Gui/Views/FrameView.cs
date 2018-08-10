//
// FrameView.cs: Frame control
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//

namespace Terminal.Gui.Views
{
    using NStack;

    using Terminal.Gui.Types;

    /// <summary>
    ///     The FrameView is a container frame that draws a frame around the contents
    /// </summary>
    public class FrameView : View
    {
        private readonly View contentView;

        private ustring title;

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:Terminal.Gui.Gui.FrameView" /> class with
        ///     an absolute position and a title.
        /// </summary>
        /// <param name="frame">Frame.</param>
        /// <param name="title">Title.</param>
        public FrameView(Rect frame, ustring title) : base(frame)
        {
            var cFrame = new Rect(1, 1, frame.Width - 2, frame.Height - 2);
            this.contentView = new ContentView(cFrame);
            base.Add(this.contentView);
            this.Title = title;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:Terminal.Gui.Gui.FrameView" /> class with
        ///     a title and the result is suitable to have its X, Y, Width and Height properties computed.
        /// </summary>
        /// <param name="title">Title.</param>
        public FrameView(ustring title)
        {
            this.contentView = new ContentView
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill(2),
                Height = Dim.Fill(2)
            };
            base.Add(this.contentView);
            this.Title = title;
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


        private void DrawFrame()
        {
            this.DrawFrame(new Rect(0, 0, this.Frame.Width, this.Frame.Height), 0, true);
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
                    this.Move(1, 0);
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
        }
    }
}