namespace Terminal.Gui {
	using NStack;

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
}