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

namespace Terminal.Gui {
	using System;

	/// <summary>
	///     ScrollBarViews are views that display a 1-character scrollbar, either horizontal or vertical
	/// </summary>
	/// <remarks>
	///     <para>
	///         The scrollbar is drawn to be a representation of the Size, assuming that the
	///         scroll position is set at Position.
	///     </para>
	///     <para>
	///         If the region to display the scrollbar is larger than three characters,
	///         arrow indicators are drawn.
	///     </para>
	/// </remarks>
	public class ScrollBarView : View {
		int size, position;

		readonly bool vertical;

		/// <summary>
		///     Initializes a new instance of the <see cref="T:Terminal.Gui.Gui.ScrollBarView" /> class.
		/// </summary>
		/// <param name="rect">Frame for the scrollbar.</param>
		/// <param name="size">The size that this scrollbar represents.</param>
		/// <param name="position">The position within this scrollbar.</param>
		/// <param name="isVertical">If set to <c>true</c> this is a vertical scrollbar, otherwize, the scrollbar is horizontal.</param>
		public ScrollBarView(Rect rect, int size, int position, bool isVertical) : base(rect)
		{
			this.vertical = isVertical;
			this.position = position;
			this.size = size;
		}

		/// <summary>
		///     The size that this scrollbar represents
		/// </summary>
		/// <value>The size.</value>
		public int Size {
			get => this.size;
			set {
				this.size = value;
				this.SetNeedsDisplay();
			}
		}

		/// <summary>
		///     The position to show the scrollbar at.
		/// </summary>
		/// <value>The position.</value>
		public int Position {
			get => this.position;
			set {
				this.position = value;
				this.SetNeedsDisplay();
			}
		}

		/// <summary>
		///     This event is raised when the position on the scrollbar has changed.
		/// </summary>
		public event Action ChangedPosition;

		void SetPosition(int newPos)
		{
			this.Position = newPos;
			this.ChangedPosition?.Invoke();
		}

		/// <summary>
		///     Redraw the scrollbar
		/// </summary>
		/// <param name="region">Region to be redrawn.</param>
		public override void Redraw(Rect region)
		{
			Driver.SetAttribute(this.ColorScheme.Normal);

			if (this.vertical) {
				if (region.Right < this.Bounds.Width - 1)
					return;

				int col = this.Bounds.Width - 1;
				int bh = this.Bounds.Height;
				Rune special;

				if (bh < 4) {
					int by1 = this.position * bh / this.Size;
					int by2 = (this.position + bh) * bh / this.Size;

					for (var y = 0; y < bh; y++) {
						this.Move(col, y);
						if (y < by1 || y > by2)
							special = Driver.Stipple;
						else
							special = Driver.Diamond;
						Driver.AddRune(special);
					}
				} else {
					bh -= 2;
					int by1 = this.position * bh / this.Size;
					int by2 = (this.position + bh) * bh / this.Size;


					this.Move(col, 0);
					Driver.AddRune('^');
					this.Move(col, this.Bounds.Height - 1);
					Driver.AddRune('v');
					for (var y = 0; y < bh; y++) {
						this.Move(col, y + 1);

						if (y < by1 || y > by2)
							special = Driver.Stipple;
						else {
							if (by2 - by1 == 0)
								special = Driver.Diamond;
							else {
								if (y == by1)
									special = Driver.TopTee;
								else if (y == by2)
									special = Driver.BottomTee;
								else
									special = Driver.VLine;
							}
						}

						Driver.AddRune(special);
					}
				}
			} else {
				if (region.Bottom < this.Bounds.Height - 1)
					return;

				int row = this.Bounds.Height - 1;
				int bw = this.Bounds.Width;
				Rune special;

				if (bw < 4) {
					int bx1 = this.position * bw / this.Size;
					int bx2 = (this.position + bw) * bw / this.Size;

					for (var x = 0; x < bw; x++) {
						this.Move(0, x);
						if (x < bx1 || x > bx2)
							special = Driver.Stipple;
						else
							special = Driver.Diamond;
						Driver.AddRune(special);
					}
				} else {
					bw -= 2;
					int bx1 = this.position * bw / this.Size;
					int bx2 = (this.position + bw) * bw / this.Size;

					this.Move(0, row);
					Driver.AddRune('<');

					for (var x = 0; x < bw; x++) {
						if (x < bx1 || x > bx2)
							special = Driver.Stipple;
						else {
							if (bx2 - bx1 == 0)
								special = Driver.Diamond;
							else {
								if (x == bx1)
									special = Driver.LeftTee;
								else if (x == bx2)
									special = Driver.RightTee;
								else
									special = Driver.HLine;
							}
						}

						Driver.AddRune(special);
					}

					Driver.AddRune('>');
				}
			}
		}

		public override bool MouseEvent(MouseEvent me)
		{
			if (me.Flags != MouseFlags.Button1Clicked)
				return false;

			int location = this.vertical ? me.Y : me.X;
			int barsize = this.vertical ? this.Bounds.Height : this.Bounds.Width;

			if (barsize < 4)
				Console.WriteLine("TODO at ScrollBarView2");
			else {
				barsize -= 2;
				// Handle scrollbars with arrow buttons
				int pos = this.Position;
				if (location == 0) {
					if (pos > 0)
						this.SetPosition(pos - 1);
				} else if (location == this.Bounds.Width - 1) {
					if (pos + 1 + barsize < this.Size)
						this.SetPosition(pos + 1);
				} else
					Console.WriteLine("TODO at ScrollBarView");
			}

			return true;
		}
	}

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
	public class ScrollView : View {
		Point contentOffset;

		Size contentSize;

		readonly View contentView;

		bool showHorizontalScrollIndicator;

		bool showVerticalScrollIndicator;

		readonly ScrollBarView vertical;

		readonly ScrollBarView horizontal;

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
		public Size ContentSize {
			get => this.contentSize;
			set {
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
		public Point ContentOffset {
			get => this.contentOffset;
			set {
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
		public bool ShowHorizontalScrollIndicator {
			get => this.showHorizontalScrollIndicator;
			set {
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
		public bool ShowVerticalScrollIndicator {
			get => this.showVerticalScrollIndicator;
			set {
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
			var oldClip = this.ClipToBounds();
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
			if (this.contentOffset.Y < 0) {
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
			if (this.contentOffset.X < 0) {
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

			switch (kb.Key) {
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