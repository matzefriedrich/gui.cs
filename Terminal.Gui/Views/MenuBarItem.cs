namespace Terminal.Gui.Views
{
    using NStack;

    /// <summary>
    ///     A menu bar item contains other menu items.
    /// </summary>
    public class MenuBarItem
    {
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

        private void SetTitle(ustring title)
        {
            if (title == null)
                title = "";
            this.Title = title;
            var len = 0;
            foreach (uint ch in this.Title)
            {
                if (ch == '_')
                    continue;
                len++;
            }

            this.TitleLength = len;
        }
    }
}