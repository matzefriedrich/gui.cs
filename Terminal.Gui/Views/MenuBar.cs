namespace Terminal.Gui {
	using System;

	/// <summary>
	///     A menu bar for your application.
	/// </summary>
	public class MenuBar : View {
		Action action;

		Menu openMenu;

		View previousFocused;

		int selected;


		/// <summary>
		///     Initializes a new instance of the <see cref="T:Terminal.Gui.MenuBar" /> class with the specified set of toplevel
		///     menu items.
		/// </summary>
		/// <param name="menus">Menus.</param>
		public MenuBar(MenuBarItem[] menus)
		{
			this.X = 0;
			this.Y = 0;
			this.Width = Dim.Fill();
			this.Height = 1;
			this.Menus = menus;
			this.CanFocus = false;
			this.selected = -1;
			this.ColorScheme = Colors.Menu;
		}

		/// <summary>
		///     The menus that were defined when the menubar was created.   This can be updated if the menu is not currently
		///     visible.
		/// </summary>
		/// <value>The menu array.</value>
		public MenuBarItem[] Menus { get; set; }

		public override void Redraw(Rect region)
		{
			this.Move(0, 0);
			Driver.SetAttribute(Colors.Base.Focus);
			for (var i = 0; i < this.Frame.Width; i++)
				Driver.AddRune(' ');

			this.Move(1, 0);
			var pos = 1;

			for (var i = 0; i < this.Menus.Length; i++) {
				var menu = this.Menus[i];
				this.Move(pos, 0);
				Attribute hotColor, normalColor;
				if (i == this.selected) {
					hotColor = i == this.selected ? this.ColorScheme.HotFocus : this.ColorScheme.HotNormal;
					normalColor = i == this.selected ? this.ColorScheme.Focus : this.ColorScheme.Normal;
				} else {
					hotColor = Colors.Base.Focus;
					normalColor = Colors.Base.Focus;
				}

				this.DrawHotString(" " + menu.Title + " " + "   ", hotColor, normalColor);
				pos += menu.TitleLength + 3;
			}

			this.PositionCursor();
		}

		public override void PositionCursor()
		{
			var pos = 0;
			for (var i = 0; i < this.Menus.Length; i++)
				if (i == this.selected) {
					pos++;
					this.Move(pos, 0);
					return;
				} else
					pos += this.Menus[i].TitleLength + 4;

			this.Move(0, 0);
		}

		void Selected(MenuItem item)
		{
			// TODO: Running = false;
			this.action = item.Action;
		}

		void OpenMenu(int index)
		{
			if (this.openMenu != null)
				this.SuperView.Remove(this.openMenu);

			var pos = 0;
			for (var i = 0; i < index; i++)
				pos += this.Menus[i].Title.Length + 3;

			this.openMenu = new Menu(this, pos, 1, this.Menus[index]);

			this.SuperView.Add(this.openMenu);
			this.SuperView.SetFocus(this.openMenu);
		}

		// Starts the menu from a hotkey
		void StartMenu()
		{
			if (this.openMenu != null)
				return;
			this.selected = 0;
			this.SetNeedsDisplay();

			this.previousFocused = this.SuperView.Focused;
			this.OpenMenu(this.selected);
		}

		// Activates the menu, handles either first focus, or activating an entry when it was already active
		// For mouse events.
		void Activate(int idx)
		{
			this.selected = idx;
			if (this.openMenu == null)
				this.previousFocused = this.SuperView.Focused;

			this.OpenMenu(idx);
			this.SetNeedsDisplay();
		}

		internal void CloseMenu()
		{
			this.selected = -1;
			this.SetNeedsDisplay();
			this.SuperView.Remove(this.openMenu);
			this.previousFocused?.SuperView?.SetFocus(this.previousFocused);
			this.openMenu = null;
		}

		internal void PreviousMenu()
		{
			if (this.selected <= 0)
				this.selected = this.Menus.Length - 1;
			else
				this.selected--;

			this.OpenMenu(this.selected);
		}

		internal void NextMenu()
		{
			if (this.selected == -1)
				this.selected = 0;
			else if (this.selected + 1 == this.Menus.Length)
				this.selected = 0;
			else
				this.selected++;
			this.OpenMenu(this.selected);
		}

		public override bool ProcessHotKey(KeyEvent kb)
		{
			if (kb.Key == Key.F9) {
				this.StartMenu();
				return true;
			}

			int kc = kb.KeyValue;

			return base.ProcessHotKey(kb);
		}

		public override bool ProcessKey(KeyEvent kb)
		{
			switch (kb.Key) {
			case Key.CursorLeft:
				this.selected--;
				if (this.selected < 0)
					this.selected = this.Menus.Length - 1;
				break;
			case Key.CursorRight:
				this.selected = (this.selected + 1) % this.Menus.Length;
				break;

			case Key.Esc:
			case Key.ControlC:
				//TODO: Running = false;
				break;

			default:
				int key = kb.KeyValue;
				if (key >= 'a' && key <= 'z' || key >= 'A' && key <= 'Z' || key >= '0' && key <= '9') {
					char c = char.ToUpper((char) key);

					if (this.Menus[this.selected].Children == null)
						return false;

					foreach (var mi in this.Menus[this.selected].Children) {
						int p = mi.Title.IndexOf('_');
						if (p != -1 && p + 1 < mi.Title.Length)
							if (mi.Title[p + 1] == c) {
								this.Selected(mi);
								return true;
							}
					}
				}

				return false;
			}

			this.SetNeedsDisplay();
			return true;
		}

		public override bool MouseEvent(MouseEvent me)
		{
			if (me.Flags == MouseFlags.Button1Clicked) {
				var pos = 1;
				int cx = me.X;
				for (var i = 0; i < this.Menus.Length; i++) {
					if (cx > pos && me.X < pos + 1 + this.Menus[i].TitleLength) {
						this.Activate(i);
						return true;
					}

					pos += 2 + this.Menus[i].TitleLength + 1;
				}
			}

			return false;
		}
	}
}