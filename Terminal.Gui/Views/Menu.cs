//
// Menu.cs: application menus and submenus
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
// TODO:
//   Add accelerator support, but should also support chords (ShortCut in MenuItem)
//   Allow menus inside menus

namespace Terminal.Gui {
	using System;

	using NStack;

	/// <summary>
	///     A menu item has a title, an associated help text, and an action to execute on activation.
	/// </summary>
	public class MenuItem {
		// 
		// 

		/// <summary>
		///     The hotkey is used when the menu is active, the shortcut can be triggered when the menu is not active.
		///     For example HotKey would be "N" when the File Menu is open (assuming there is a "_New" entry
		///     if the ShortCut is set to "Control-N", this would be a global hotkey that would trigger as well
		/// </summary>
		public Rune HotKey;

		/// <summary>
		///     This is the global setting that can be used as a global shortcut to invoke the action on the menu.
		/// </summary>
		public Key ShortCut;

		/// <summary>
		///     Initializes a new <see cref="T:Terminal.Gui.MenuItem" />.
		/// </summary>
		/// <param name="title">Title for the menu item.</param>
		/// <param name="help">Help text to display.</param>
		/// <param name="action">Action to invoke when the menu item is activated.</param>
		public MenuItem(ustring title, string help, Action action)
		{
			this.Title = title ?? "";
			this.Help = help ?? "";
			this.Action = action;
			var nextIsHot = false;
			foreach (uint x in this.Title)
				if (x == '_')
					nextIsHot = true;
				else {
					if (nextIsHot) {
						this.HotKey = x;
						break;
					}

					nextIsHot = false;
				}
		}

		/// <summary>
		///     Gets or sets the title.
		/// </summary>
		/// <value>The title.</value>
		public ustring Title { get; set; }

		/// <summary>
		///     Gets or sets the help text for the menu item.
		/// </summary>
		/// <value>The help text.</value>
		public ustring Help { get; set; }

		/// <summary>
		///     Gets or sets the action to be invoked when the menu is triggered
		/// </summary>
		/// <value>Method to invoke.</value>
		public Action Action { get; set; }

		internal int Width => this.Title.Length + this.Help.Length + 1 + 2;
	}

	/// <summary>
	///     A menu bar item contains other menu items.
	/// </summary>
	public class MenuBarItem {
		public MenuBarItem(ustring title, MenuItem[] children)
		{
			this.SetTitle(title ?? "");
			this.Children = children;
		}

		/// <summary>
		///     Gets or sets the title to display.
		/// </summary>
		/// <value>The title.</value>
		public ustring Title { get; set; }

		/// <summary>
		///     Gets or sets the children for this MenuBarItem
		/// </summary>
		/// <value>The children.</value>
		public MenuItem[] Children { get; set; }

		internal int TitleLength { get; private set; }

		void SetTitle(ustring title)
		{
			if (title == null)
				title = "";
			this.Title = title;
			var len = 0;
			foreach (uint ch in this.Title) {
				if (ch == '_')
					continue;
				len++;
			}

			this.TitleLength = len;
		}
	}

	class Menu : View {
		readonly MenuBarItem barItems;

		int current;

		readonly MenuBar host;

		public Menu(MenuBar host, int x, int y, MenuBarItem barItems) : base(MakeFrame(x, y, barItems.Children))
		{
			this.barItems = barItems;
			this.host = host;
			this.ColorScheme = Colors.Menu;
			this.CanFocus = true;
		}

		static Rect MakeFrame(int x, int y, MenuItem[] items)
		{
			var maxW = 0;

			foreach (var item in items) {
				int l = item.Width;
				maxW = Math.Max(l, maxW);
			}

			return new Rect(x, y, maxW + 2, items.Length + 2);
		}

		public override void Redraw(Rect region)
		{
			Driver.SetAttribute(this.ColorScheme.Normal);
			this.DrawFrame(region, 0, true);

			for (var i = 0; i < this.barItems.Children.Length; i++) {
				var item = this.barItems.Children[i];
				this.Move(1, i + 1);
				Driver.SetAttribute(item == null ? Colors.Base.Focus : i == this.current ? this.ColorScheme.Focus : this.ColorScheme.Normal);
				for (var p = 0; p < this.Frame.Width - 2; p++)
					if (item == null)
						Driver.AddRune(Driver.HLine);
					else
						Driver.AddRune(' ');

				if (item == null)
					continue;

				this.Move(2, i + 1);
				this.DrawHotString(item.Title,
					i == this.current ? this.ColorScheme.HotFocus : this.ColorScheme.HotNormal,
					i == this.current ? this.ColorScheme.Focus : this.ColorScheme.Normal);

				// The help string
				int l = item.Help.Length;
				this.Move(this.Frame.Width - l - 2, 1 + i);
				Driver.AddStr(item.Help);
			}
		}

		public override void PositionCursor()
		{
			this.Move(2, 1 + this.current);
		}

		void Run(Action action)
		{
			if (action == null)
				return;

			Application.MainLoop.AddIdle(() => {
				action();
				return false;
			});
		}

		public override bool ProcessKey(KeyEvent kb)
		{
			switch (kb.Key) {
			case Key.CursorUp:
				this.current--;
				if (this.current < 0)
					this.current = this.barItems.Children.Length - 1;
				this.SetNeedsDisplay();
				break;
			case Key.CursorDown:
				this.current++;
				if (this.current == this.barItems.Children.Length)
					this.current = 0;
				this.SetNeedsDisplay();
				break;
			case Key.CursorLeft:
				this.host.PreviousMenu();
				break;
			case Key.CursorRight:
				this.host.NextMenu();
				break;
			case Key.Esc:
				this.host.CloseMenu();
				break;
			case Key.Enter:
				this.host.CloseMenu();
				this.Run(this.barItems.Children[this.current].Action);
				break;
			default:
				// TODO: rune-ify
				if (char.IsLetterOrDigit((char) kb.KeyValue)) {
					char x = char.ToUpper((char) kb.KeyValue);

					foreach (var item in this.barItems.Children)
						if (item.HotKey == x) {
							this.host.CloseMenu();
							this.Run(item.Action);
							return true;
						}
				}

				break;
			}

			return true;
		}

		public override bool MouseEvent(MouseEvent me)
		{
			if (me.Flags == MouseFlags.Button1Clicked || me.Flags == MouseFlags.Button1Released) {
				if (me.Y < 1)
					return true;
				int item = me.Y - 1;
				if (item >= this.barItems.Children.Length)
					return true;
				this.host.CloseMenu();
				this.Run(this.barItems.Children[item].Action);
				return true;
			}

			if (me.Flags == MouseFlags.Button1Pressed) {
				if (me.Y < 1)
					return true;
				if (me.Y - 1 >= this.barItems.Children.Length)
					return true;
				this.current = me.Y - 1;
				this.SetNeedsDisplay();
				return true;
			}

			return false;
		}
	}

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