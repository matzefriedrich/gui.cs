namespace Terminal.Gui
{
    /// <summary>
    ///     Implement this interface to provide your own custom rendering for a list.
    /// </summary>
    public interface IListDataSource
    {
        /// <summary>
        ///     Returns the number of elements to display
        /// </summary>
        int Count { get; }

        /// <summary>
        ///     This method is invoked to render a specified item, the method should cover the entire provided width.
        /// </summary>
        /// <returns>The render.</returns>
        /// <param name="selected">Describes whether the item being rendered is currently selected by the user.</param>
        /// <param name="item">The index of the item to render, zero for the first item and so on.</param>
        /// <param name="col">The column where the rendering will start</param>
        /// <param name="line">The line where the rendering will be done.</param>
        /// <param name="width">The width that must be filled out.</param>
        /// <remarks>
        ///     The default color will be set before this method is invoked, and will be based on whether the item is selected or
        ///     not.
        /// </remarks>
        void Render(bool selected, int item, int col, int line, int width);

        /// <summary>
        ///     Should return whether the specified item is currently marked.
        /// </summary>
        /// <returns><c>true</c>, if marked, <c>false</c> otherwise.</returns>
        /// <param name="item">Item index.</param>
        bool IsMarked(int item);

        /// <summary>
        ///     Flags the item as marked.
        /// </summary>
        /// <param name="item">Item index.</param>
        /// <param name="value">If set to <c>true</c> value.</param>
        void SetMark(int item, bool value);
    }
}