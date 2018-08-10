namespace Terminal.Gui
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using NStack;

    internal class DirListView : View
    {
        private string[] allowedFileTypes;

        internal bool allowsMultipleSelection;

        internal bool canChooseDirectories;

        internal bool canChooseFiles = true;

        private ustring directory;

        public Action<ustring> DirectoryChanged;

        private DirectoryInfo dirInfo;

        public Action<ustring> FileChanged;

        private List<(string, bool, bool)> infos;

        public Action<(string, bool)> SelectedChanged;

        private int top, selected;

        public DirListView()
        {
            this.infos = new List<(string, bool, bool)>();
            this.CanFocus = true;
        }

        public ustring Directory
        {
            get => this.directory;
            set
            {
                if (this.directory == value)
                    return;
                this.directory = value;
                this.Reload();
            }
        }

        public string[] AllowedFileTypes
        {
            get => this.allowedFileTypes;
            set
            {
                this.allowedFileTypes = value;
                this.Reload();
            }
        }

        public IReadOnlyList<string> FilePaths
        {
            get
            {
                if (this.allowsMultipleSelection)
                {
                    var res = new List<string>();
                    foreach ((string, bool, bool) item in this.infos)
                        if (item.Item3)
                            res.Add(this.MakePath(item.Item1));
                    return res;
                }

                if (this.infos[this.selected].Item2)
                {
                    if (this.canChooseDirectories)
                        return new List<string> {this.MakePath(this.infos[this.selected].Item1)};
                    return Array.Empty<string>();
                }

                if (this.canChooseFiles)
                    return new List<string> {this.MakePath(this.infos[this.selected].Item1)};
                return Array.Empty<string>();
            }
        }

        private bool IsAllowed(FileSystemInfo fsi)
        {
            if (fsi.Attributes.HasFlag(FileAttributes.Directory))
                return true;
            if (this.allowedFileTypes == null)
                return true;
            foreach (string ft in this.allowedFileTypes)
                if (fsi.Name.EndsWith(ft))
                    return true;
            return false;
        }

        internal void Reload()
        {
            this.dirInfo = new DirectoryInfo(this.directory.ToString());
            this.infos = (from x in this.dirInfo.GetFileSystemInfos()
                where this.IsAllowed(x)
                orderby !x.Attributes.HasFlag(FileAttributes.Directory) + x.Name
                select (x.Name, x.Attributes.HasFlag(FileAttributes.Directory), false)).ToList();
            this.infos.Insert(0, ("..", true, false));
            this.top = 0;
            this.selected = 0;
            this.SetNeedsDisplay();
        }

        public override void PositionCursor()
        {
            this.Move(0, this.selected - this.top);
        }

        private void DrawString(int line, string str)
        {
            Rect f = this.Frame;
            int width = f.Width;
            ustring ustr = ustring.Make(str);

            this.Move(this.allowsMultipleSelection ? 3 : 2, line);
            int byteLen = ustr.Length;
            var used = 0;
            for (var i = 0; i < byteLen;)
            {
                (uint rune, int size) = Utf8.DecodeRune(ustr, i, i - byteLen);
                int count = Rune.ColumnWidth(rune);
                if (used + count >= width)
                    break;
                Driver.AddRune(rune);
                used += count;
                i += size;
            }

            for (; used < width; used++)
                Driver.AddRune(' ');
        }

        public override void Redraw(Rect region)
        {
            Attribute current = this.ColorScheme.Focus;
            Driver.SetAttribute(current);
            this.Move(0, 0);
            Rect f = this.Frame;
            int item = this.top;
            bool focused = this.HasFocus;
            int width = region.Width;

            for (var row = 0; row < f.Height; row++, item++)
            {
                bool isSelected = item == this.selected;
                this.Move(0, row);
                Attribute newcolor = focused ? (isSelected ? this.ColorScheme.HotNormal : this.ColorScheme.Focus) : this.ColorScheme.Focus;
                if (newcolor != current)
                {
                    Driver.SetAttribute(newcolor);
                    current = newcolor;
                }

                if (item >= this.infos.Count)
                {
                    for (var c = 0; c < f.Width; c++)
                        Driver.AddRune(' ');
                    continue;
                }

                (string, bool, bool) fi = this.infos[item];

                Driver.AddRune(isSelected ? '>' : ' ');

                if (this.allowsMultipleSelection)
                    Driver.AddRune(fi.Item3 ? '*' : ' ');

                if (fi.Item2)
                    Driver.AddRune('/');
                else
                    Driver.AddRune(' ');
                this.DrawString(row, fi.Item1);
            }
        }

        private void SelectionChanged()
        {
            if (this.SelectedChanged != null)
            {
                (string, bool, bool) sel = this.infos[this.selected];
                this.SelectedChanged((sel.Item1, sel.Item2));
            }
        }

        public override bool ProcessKey(KeyEvent keyEvent)
        {
            switch (keyEvent.Key)
            {
                case Key.CursorUp:
                case Key.ControlP:
                    if (this.selected > 0)
                    {
                        this.selected--;
                        if (this.selected < this.top)
                            this.top = this.selected;
                        this.SelectionChanged();
                        this.SetNeedsDisplay();
                    }

                    return true;

                case Key.CursorDown:
                case Key.ControlN:
                    if (this.selected + 1 < this.infos.Count)
                    {
                        this.selected++;
                        if (this.selected >= this.top + this.Frame.Height)
                            this.top++;
                        this.SelectionChanged();
                        this.SetNeedsDisplay();
                    }

                    return true;

                case Key.ControlV:
                case Key.PageDown:
                    int n = this.selected + this.Frame.Height;
                    if (n > this.infos.Count)
                        n = this.infos.Count - 1;
                    if (n != this.selected)
                    {
                        this.selected = n;
                        if (this.infos.Count >= this.Frame.Height)
                            this.top = this.selected;
                        else
                            this.top = 0;
                        this.SelectionChanged();

                        this.SetNeedsDisplay();
                    }

                    return true;

                case Key.Enter:
                    bool isDir = this.infos[this.selected].Item2;

                    if (isDir)
                    {
                        this.Directory = Path.GetFullPath(Path.Combine(Path.GetFullPath(this.Directory.ToString()), this.infos[this.selected].Item1));
                        if (this.DirectoryChanged != null)
                            this.DirectoryChanged(this.Directory);
                    }
                    else
                    {
                        if (this.FileChanged != null)
                            this.FileChanged(this.infos[this.selected].Item1);
                        if (this.canChooseFiles)
                            return false;
                        // No files allowed, do not let the default handler take it.
                    }

                    return true;

                case Key.PageUp:
                    n = this.selected - this.Frame.Height;
                    if (n < 0)
                        n = 0;
                    if (n != this.selected)
                    {
                        this.selected = n;
                        this.top = this.selected;
                        this.SelectionChanged();
                        this.SetNeedsDisplay();
                    }

                    return true;

                case Key.Space:
                case Key.ControlT:
                    if (this.allowsMultipleSelection)
                        if (this.canChooseFiles && this.infos[this.selected].Item2 == false ||
                            this.canChooseDirectories && this.infos[this.selected].Item2 && this.infos[this.selected].Item1 != "..")
                        {
                            this.infos[this.selected] = (this.infos[this.selected].Item1, this.infos[this.selected].Item2, !this.infos[this.selected].Item3);
                            this.SelectionChanged();
                            this.SetNeedsDisplay();
                        }

                    return true;
            }

            return base.ProcessKey(keyEvent);
        }

        public string MakePath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(this.Directory.ToString(), relativePath));
        }
    }
}