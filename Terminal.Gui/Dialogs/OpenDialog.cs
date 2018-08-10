namespace Terminal.Gui.Dialogs
{
    using System.Collections.Generic;

    using NStack;

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
    public class OpenDialog : FileDialog
    {
        public OpenDialog(ustring title, ustring message) : base(title, "Open", "Open", message)
        {
        }

        /// <summary>
        ///     Gets or sets a value indicating whether this <see cref="T:Terminal.Gui.Dialogs.OpenDialog" /> can choose files.
        /// </summary>
        /// <value><c>true</c> if can choose files; otherwise, <c>false</c>.  Defaults to <c>true</c></value>
        public bool CanChooseFiles
        {
            get => this.dirListView.canChooseFiles;
            set
            {
                this.dirListView.canChooseDirectories = value;
                this.dirListView.Reload();
            }
        }

        /// <summary>
        ///     Gets or sets a value indicating whether this <see cref="T:Terminal.Gui.Dialogs.OpenDialog" /> can choose directories.
        /// </summary>
        /// <value><c>true</c> if can choose directories; otherwise, <c>false</c> defaults to <c>false</c>.</value>
        public bool CanChooseDirectories
        {
            get => this.dirListView.canChooseDirectories;
            set
            {
                this.dirListView.canChooseDirectories = value;
                this.dirListView.Reload();
            }
        }

        /// <summary>
        ///     Gets or sets a value indicating whether this <see cref="T:Terminal.Gui.Dialogs.OpenDialog" /> allows multiple selection.
        /// </summary>
        /// <value><c>true</c> if allows multiple selection; otherwise, <c>false</c>, defaults to false.</value>
        public bool AllowsMultipleSelection
        {
            get => this.dirListView.allowsMultipleSelection;
            set
            {
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