// 
// FileDialog.cs: File system dialogs for open and save
//
// TODO:
//   * Add directory selector
//   * Implement subclasses
//   * Figure out why message text does not show
//   * Remove the extra space when message does not show
//   * Use a line separator to show the file listing, so we can use same colors as the rest
//   * DirListView: Add mouse support

namespace Terminal.Gui {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	using NStack;

	class DirListView : View {
		string[] allowedFileTypes;

		internal bool allowsMultipleSelection;

		internal bool canChooseDirectories;

		internal bool canChooseFiles = true;

		ustring directory;

		public Action<ustring> DirectoryChanged;

		DirectoryInfo dirInfo;

		public Action<ustring> FileChanged;

		List<(string, bool, bool)> infos;

		public Action<(string, bool)> SelectedChanged;

		int top, selected;

		public DirListView()
		{
			this.infos = new List<(string, bool, bool)>();
			this.CanFocus = true;
		}

		public ustring Directory {
			get => this.directory;
			set {
				if (this.directory == value)
					return;
				this.directory = value;
				this.Reload();
			}
		}

		public string[] AllowedFileTypes {
			get => this.allowedFileTypes;
			set {
				this.allowedFileTypes = value;
				this.Reload();
			}
		}

		public IReadOnlyList<string> FilePaths {
			get {
				if (this.allowsMultipleSelection) {
					var res = new List<string>();
					foreach (var item in this.infos)
						if (item.Item3)
							res.Add(this.MakePath(item.Item1));
					return res;
				}

				if (this.infos[this.selected].Item2) {
					if (this.canChooseDirectories)
						return new List<string> {this.MakePath(this.infos[this.selected].Item1)};
					return Array.Empty<string>();
				}

				if (this.canChooseFiles)
					return new List<string> {this.MakePath(this.infos[this.selected].Item1)};
				return Array.Empty<string>();
			}
		}

		bool IsAllowed(FileSystemInfo fsi)
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

		void DrawString(int line, string str)
		{
			var f = this.Frame;
			int width = f.Width;
			var ustr = ustring.Make(str);

			this.Move(this.allowsMultipleSelection ? 3 : 2, line);
			int byteLen = ustr.Length;
			var used = 0;
			for (var i = 0; i < byteLen;) {
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
			var current = this.ColorScheme.Focus;
			Driver.SetAttribute(current);
			this.Move(0, 0);
			var f = this.Frame;
			int item = this.top;
			bool focused = this.HasFocus;
			int width = region.Width;

			for (var row = 0; row < f.Height; row++, item++) {
				bool isSelected = item == this.selected;
				this.Move(0, row);
				var newcolor = focused ? (isSelected ? this.ColorScheme.HotNormal : this.ColorScheme.Focus) : this.ColorScheme.Focus;
				if (newcolor != current) {
					Driver.SetAttribute(newcolor);
					current = newcolor;
				}

				if (item >= this.infos.Count) {
					for (var c = 0; c < f.Width; c++)
						Driver.AddRune(' ');
					continue;
				}

				var fi = this.infos[item];

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

		void SelectionChanged()
		{
			if (this.SelectedChanged != null) {
				var sel = this.infos[this.selected];
				this.SelectedChanged((sel.Item1, sel.Item2));
			}
		}

		public override bool ProcessKey(KeyEvent keyEvent)
		{
			switch (keyEvent.Key) {
			case Key.CursorUp:
			case Key.ControlP:
				if (this.selected > 0) {
					this.selected--;
					if (this.selected < this.top)
						this.top = this.selected;
					this.SelectionChanged();
					this.SetNeedsDisplay();
				}

				return true;

			case Key.CursorDown:
			case Key.ControlN:
				if (this.selected + 1 < this.infos.Count) {
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
				if (n != this.selected) {
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

				if (isDir) {
					this.Directory = Path.GetFullPath(Path.Combine(Path.GetFullPath(this.Directory.ToString()), this.infos[this.selected].Item1));
					if (this.DirectoryChanged != null)
						this.DirectoryChanged(this.Directory);
				} else {
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
				if (n != this.selected) {
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
					    this.canChooseDirectories && this.infos[this.selected].Item2 && this.infos[this.selected].Item1 != "..") {
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

	/// <summary>
	///     Base class for the OpenDialog and the SaveDialog
	/// </summary>
	public class FileDialog : Dialog {
		internal bool canceled;

		readonly TextField dirEntry;

		readonly TextField nameEntry;

		internal DirListView dirListView;

		readonly Label nameFieldLabel;

		readonly Label message;

		readonly Label dirLabel;

		readonly Button prompt;

		readonly Button cancel;

		public FileDialog(ustring title, ustring prompt, ustring nameFieldLabel, ustring message) : base(title, Driver.Cols - 20, Driver.Rows - 5, null)
		{
			this.message = new Label(Rect.Empty, "MESSAGE" + message);
			int msgLines = Label.MeasureLines(message, Driver.Cols - 20);

			this.dirLabel = new Label("Directory: ") {
				X = 1,
				Y = 1 + msgLines
			};

			this.dirEntry = new TextField("") {
				X = Pos.Right(this.dirLabel),
				Y = 1 + msgLines,
				Width = Dim.Fill() - 1
			};
			this.Add(this.dirLabel, this.dirEntry);

			this.nameFieldLabel = new Label("Open: ") {
				X = 6,
				Y = 3 + msgLines
			};
			this.nameEntry = new TextField("") {
				X = Pos.Left(this.dirEntry),
				Y = 3 + msgLines,
				Width = Dim.Fill() - 1
			};
			this.Add(this.nameFieldLabel, this.nameEntry);

			this.dirListView = new DirListView {
				X = 1,
				Y = 3 + msgLines + 2,
				Width = Dim.Fill(),
				Height = Dim.Fill() - 2
			};
			this.DirectoryPath = Path.GetFullPath(Environment.CurrentDirectory);
			this.Add(this.dirListView);
			this.dirListView.DirectoryChanged = dir => this.dirEntry.Text = dir;
			this.dirListView.FileChanged = file => { this.nameEntry.Text = file; };

			this.cancel = new Button("Cancel");
			this.cancel.Clicked += () => {
				this.canceled = true;
				Application.RequestStop();
			};
			this.AddButton(this.cancel);

			this.prompt = new Button(prompt) {
				IsDefault = true
			};
			this.prompt.Clicked += () => {
				this.canceled = false;
				Application.RequestStop();
			};
			this.AddButton(this.prompt);

			// On success, we will set this to false.
			this.canceled = true;
		}

		/// <summary>
		///     Gets or sets the prompt label for the button displayed to the user
		/// </summary>
		/// <value>The prompt.</value>
		public ustring Prompt {
			get => this.prompt.Text;
			set => this.prompt.Text = value;
		}

		/// <summary>
		///     Gets or sets the name field label.
		/// </summary>
		/// <value>The name field label.</value>
		public ustring NameFieldLabel {
			get => this.nameFieldLabel.Text;
			set => this.nameFieldLabel.Text = value;
		}

		/// <summary>
		///     Gets or sets the message displayed to the user, defaults to nothing
		/// </summary>
		/// <value>The message.</value>
		public ustring Message {
			get => this.message.Text;
			set => this.message.Text = value;
		}

		/// <summary>
		///     Gets or sets a value indicating whether this <see cref="T:Terminal.Gui.FileDialog" /> can create directories.
		/// </summary>
		/// <value><c>true</c> if can create directories; otherwise, <c>false</c>.</value>
		public bool CanCreateDirectories { get; set; }

		/// <summary>
		///     Gets or sets a value indicating whether this <see cref="T:Terminal.Gui.FileDialog" /> is extension hidden.
		/// </summary>
		/// <value><c>true</c> if is extension hidden; otherwise, <c>false</c>.</value>
		public bool IsExtensionHidden { get; set; }

		/// <summary>
		///     Gets or sets the directory path for this panel
		/// </summary>
		/// <value>The directory path.</value>
		public ustring DirectoryPath {
			get => this.dirEntry.Text;
			set {
				this.dirEntry.Text = value;
				this.dirListView.Directory = value;
			}
		}

		/// <summary>
		///     The array of filename extensions allowed, or null if all file extensions are allowed.
		/// </summary>
		/// <value>The allowed file types.</value>
		public string[] AllowedFileTypes {
			get => this.dirListView.AllowedFileTypes;
			set => this.dirListView.AllowedFileTypes = value;
		}


		/// <summary>
		///     Gets or sets a value indicating whether this <see cref="T:Terminal.Gui.FileDialog" /> allows the file to be saved
		///     with a different extension
		/// </summary>
		/// <value><c>true</c> if allows other file types; otherwise, <c>false</c>.</value>
		public bool AllowsOtherFileTypes { get; set; }

		/// <summary>
		///     The File path that is currently shown on the panel
		/// </summary>
		/// <value>The absolute file path for the file path entered.</value>
		public ustring FilePath {
			get => this.nameEntry.Text;
			set => this.nameEntry.Text = value;
		}

		public override void WillPresent()
		{
			base.WillPresent();
			//SetFocus (nameEntry);
		}
	}

	/// <summary>
	///     The save dialog provides an interactive dialog box for users to pick a file to
	///     save.
	/// </summary>
	/// <remarks>
	///     <para>
	///         To use it, create an instance of the SaveDialog, and then
	///         call Application.Run on the resulting instance.   This will run the dialog modally,
	///         and when this returns, the FileName property will contain the selected value or
	///         null if the user canceled.
	///     </para>
	/// </remarks>
	public class SaveDialog : FileDialog {
		public SaveDialog(ustring title, ustring message) : base(title, "Save", "Save as:", message)
		{
		}

		/// <summary>
		///     Gets the name of the file the user selected for saving, or null
		///     if the user canceled the dialog box.
		/// </summary>
		/// <value>The name of the file.</value>
		public ustring FileName {
			get {
				if (this.canceled)
					return null;
				return this.FilePath;
			}
		}
	}

	/// <summary>
	///     The Open Dialog provides an interactive dialog box for users to select files or directories.
	/// </summary>
	/// <remarks>
	///     <para>
	///         The open dialog can be used to select files for opening, it can be configured to allow
	///         multiple items to be selected (based on the AllowsMultipleSelection) variable and
	///         you can control whether this should allow files or directories to be selected.
	///     </para>
	///     <para>
	///         To use it, create an instance of the OpenDialog, configure its properties, and then
	///         call Application.Run on the resulting instance.   This will run the dialog modally,
	///         and when this returns, the list of filds will be available on the FilePaths property.
	///     </para>
	///     <para>
	///         To select more than one file, users can use the spacebar, or control-t.
	///     </para>
	/// </remarks>
	public class OpenDialog : FileDialog {
		public OpenDialog(ustring title, ustring message) : base(title, "Open", "Open", message)
		{
		}

		/// <summary>
		///     Gets or sets a value indicating whether this <see cref="T:Terminal.Gui.OpenDialog" /> can choose files.
		/// </summary>
		/// <value><c>true</c> if can choose files; otherwise, <c>false</c>.  Defaults to <c>true</c></value>
		public bool CanChooseFiles {
			get => this.dirListView.canChooseFiles;
			set {
				this.dirListView.canChooseDirectories = value;
				this.dirListView.Reload();
			}
		}

		/// <summary>
		///     Gets or sets a value indicating whether this <see cref="T:Terminal.Gui.OpenDialog" /> can choose directories.
		/// </summary>
		/// <value><c>true</c> if can choose directories; otherwise, <c>false</c> defaults to <c>false</c>.</value>
		public bool CanChooseDirectories {
			get => this.dirListView.canChooseDirectories;
			set {
				this.dirListView.canChooseDirectories = value;
				this.dirListView.Reload();
			}
		}

		/// <summary>
		///     Gets or sets a value indicating whether this <see cref="T:Terminal.Gui.OpenDialog" /> allows multiple selection.
		/// </summary>
		/// <value><c>true</c> if allows multiple selection; otherwise, <c>false</c>, defaults to false.</value>
		public bool AllowsMultipleSelection {
			get => this.dirListView.allowsMultipleSelection;
			set {
				this.dirListView.allowsMultipleSelection = value;
				this.dirListView.Reload();
			}
		}

		/// <summary>
		///     Returns the selected files, or an empty list if nothing has been selected
		/// </summary>
		/// <value>The file paths.</value>
		public IReadOnlyList<string> FilePaths => this.dirListView.FilePaths;
	}
}