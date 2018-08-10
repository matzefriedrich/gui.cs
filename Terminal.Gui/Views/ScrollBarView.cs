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
		readonly bool vertical;

		int size, position;

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
}