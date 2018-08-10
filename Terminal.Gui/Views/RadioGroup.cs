namespace Terminal.Gui {
	using System;

	/// <summary>
	///     Radio group shows a group of labels, only one of those can be selected at a given time
	/// </summary>
	public class RadioGroup : View {
		string[] radioLabels;

		int selected, cursor;

		public Action<int> SelectionChanged;

		/// <summary>
		///     Initializes a new instance of the <see cref="T:Terminal.Gui.RadioGroup" /> class
		///     setting up the initial set of radio labels and the item that should be selected and uses
		///     an absolute layout for the result.
		/// </summary>
		/// <param name="rect">Boundaries for the radio group.</param>
		/// <param name="radioLabels">Radio labels, the strings can contain hotkeys using an undermine before the letter.</param>
		/// <param name="selected">The item to be selected, the value is clamped to the number of items.</param>
		public RadioGroup(Rect rect, string[] radioLabels, int selected = 0) : base(rect)
		{
			this.selected = selected;
			this.radioLabels = radioLabels;
			this.CanFocus = true;
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="T:Terminal.Gui.RadioGroup" /> class
		///     setting up the initial set of radio labels and the item that should be selected.
		/// </summary>
		/// <param name="radioLabels">Radio labels, the strings can contain hotkeys using an undermine before the letter.</param>
		/// <param name="selected">The item to be selected, the value is clamped to the number of items.</param>
		public RadioGroup(string[] radioLabels, int selected = 0)
		{
			var r = MakeRect(0, 0, radioLabels);
			this.Width = r.Width;
			this.Height = radioLabels.Length;

			this.selected = selected;
			this.radioLabels = radioLabels;
			this.CanFocus = true;
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="T:Terminal.Gui.RadioGroup" /> class
		///     setting up the initial set of radio labels and the item that should be selected,
		///     the view frame is computed from the provided radioLabels.
		/// </summary>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		/// <param name="radioLabels">Radio labels, the strings can contain hotkeys using an undermine before the letter.</param>
		/// <param name="selected">The item to be selected, the value is clamped to the number of items.</param>
		public RadioGroup(int x, int y, string[] radioLabels, int selected = 0) : this(MakeRect(x, y, radioLabels), radioLabels, selected)
		{
		}

		/// <summary>
		///     The radio labels to display
		/// </summary>
		/// <value>The radio labels.</value>
		public string[] RadioLabels {
			get => this.radioLabels;
			set {
				this.radioLabels = value;
				this.selected = 0;
				this.cursor = 0;
				this.SetNeedsDisplay();
			}
		}

		/// <summary>
		///     The currently selected item from the list of radio labels
		/// </summary>
		/// <value>The selected.</value>
		public int Selected {
			get => this.selected;
			set {
				this.selected = value;
				this.SelectionChanged?.Invoke(this.selected);
				this.SetNeedsDisplay();
			}
		}

		static Rect MakeRect(int x, int y, string[] radioLabels)
		{
			var width = 0;

			foreach (string s in radioLabels)
				width = Math.Max(s.Length + 4, width);
			return new Rect(x, y, width, radioLabels.Length);
		}

		public override void Redraw(Rect region)
		{
			base.Redraw(region);
			for (var i = 0; i < this.radioLabels.Length; i++) {
				this.Move(0, i);
				Driver.SetAttribute(this.ColorScheme.Normal);
				Driver.AddStr(i == this.selected ? "(o) " : "( ) ");
				this.DrawHotString(this.radioLabels[i], this.HasFocus && i == this.cursor, this.ColorScheme);
			}
		}

		public override void PositionCursor()
		{
			this.Move(1, this.cursor);
		}

		public override bool ProcessColdKey(KeyEvent kb)
		{
			int key = kb.KeyValue;
			if (key < char.MaxValue && char.IsLetterOrDigit((char) key)) {
				var i = 0;
				key = char.ToUpper((char) key);
				foreach (string l in this.radioLabels) {
					var nextIsHot = false;
					foreach (char c in l)
						if (c == '_')
							nextIsHot = true;
						else {
							if (nextIsHot && c == key) {
								this.Selected = i;
								this.cursor = i;
								if (!this.HasFocus)
									this.SuperView.SetFocus(this);
								return true;
							}

							nextIsHot = false;
						}

					i++;
				}
			}

			return false;
		}

		public override bool ProcessKey(KeyEvent kb)
		{
			switch (kb.Key) {
			case Key.CursorUp:
				if (this.cursor > 0) {
					this.cursor--;
					this.SetNeedsDisplay();
					return true;
				}

				break;
			case Key.CursorDown:
				if (this.cursor + 1 < this.radioLabels.Length) {
					this.cursor++;
					this.SetNeedsDisplay();
					return true;
				}

				break;
			case Key.Space:
				this.Selected = this.cursor;
				return true;
			}

			return base.ProcessKey(kb);
		}

		public override bool MouseEvent(MouseEvent me)
		{
			if (!me.Flags.HasFlag(MouseFlags.Button1Clicked))
				return false;

			this.SuperView.SetFocus(this);

			if (me.Y < this.radioLabels.Length) {
				this.cursor = this.Selected = me.Y;
				this.SetNeedsDisplay();
			}

			return true;
		}
	}
}