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
}