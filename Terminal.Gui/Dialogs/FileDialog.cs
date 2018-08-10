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

namespace Terminal.Gui
{
    using System;
    using System.IO;

    using NStack;

    /// <summary>
    ///     Base class for the OpenDialog and the SaveDialog
    /// </summary>
    public class FileDialog : Dialog
    {
        private readonly Button cancel;

        private readonly TextField dirEntry;

        private readonly Label dirLabel;

        private readonly Label message;

        private readonly TextField nameEntry;

        private readonly Label nameFieldLabel;

        private readonly Button prompt;

        internal bool canceled;

        internal DirListView dirListView;

        public FileDialog(ustring title, ustring prompt, ustring nameFieldLabel, ustring message) : base(title, Driver.Cols - 20, Driver.Rows - 5, null)
        {
            this.message = new Label(Rect.Empty, "MESSAGE" + message);
            int msgLines = Label.MeasureLines(message, Driver.Cols - 20);

            this.dirLabel = new Label("Directory: ")
            {
                X = 1,
                Y = 1 + msgLines
            };

            this.dirEntry = new TextField("")
            {
                X = Pos.Right(this.dirLabel),
                Y = 1 + msgLines,
                Width = Dim.Fill() - 1
            };
            this.Add(this.dirLabel, this.dirEntry);

            this.nameFieldLabel = new Label("Open: ")
            {
                X = 6,
                Y = 3 + msgLines
            };
            this.nameEntry = new TextField("")
            {
                X = Pos.Left(this.dirEntry),
                Y = 3 + msgLines,
                Width = Dim.Fill() - 1
            };
            this.Add(this.nameFieldLabel, this.nameEntry);

            this.dirListView = new DirListView
            {
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
            this.cancel.Clicked += () =>
            {
                this.canceled = true;
                Application.RequestStop();
            };
            this.AddButton(this.cancel);

            this.prompt = new Button(prompt)
            {
                IsDefault = true
            };
            this.prompt.Clicked += () =>
            {
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
        public ustring Prompt
        {
            get => this.prompt.Text;
            set => this.prompt.Text = value;
        }

        /// <summary>
        ///     Gets or sets the name field label.
        /// </summary>
        /// <value>The name field label.</value>
        public ustring NameFieldLabel
        {
            get => this.nameFieldLabel.Text;
            set => this.nameFieldLabel.Text = value;
        }

        /// <summary>
        ///     Gets or sets the message displayed to the user, defaults to nothing
        /// </summary>
        /// <value>The message.</value>
        public ustring Message
        {
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
        public ustring DirectoryPath
        {
            get => this.dirEntry.Text;
            set
            {
                this.dirEntry.Text = value;
                this.dirListView.Directory = value;
            }
        }

        /// <summary>
        ///     The array of filename extensions allowed, or null if all file extensions are allowed.
        /// </summary>
        /// <value>The allowed file types.</value>
        public string[] AllowedFileTypes
        {
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
        public ustring FilePath
        {
            get => this.nameEntry.Text;
            set => this.nameEntry.Text = value;
        }

        public override void WillPresent()
        {
            base.WillPresent();
            //SetFocus (nameEntry);
        }
    }
}