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

	class Menu : View {
		readonly MenuBarItem barItems;

		readonly MenuBar host;

		int current;

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
}