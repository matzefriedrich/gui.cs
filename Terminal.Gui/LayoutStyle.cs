namespace Terminal.Gui
{
    /// <summary>
    ///     Determines the LayoutStyle for a view, if Absolute, during LayoutSubviews, the
    ///     value from the Frame will be used, if the value is Computer, then the Frame
    ///     will be updated from the X, Y Pos objets and the Width and Heigh Dim objects.
    /// </summary>
    public enum LayoutStyle
    {
        /// <summary>
        ///     The position and size of the view are based on the Frame value.
        /// </summary>
        Absolute,

        /// <summary>
        ///     The position and size of the view will be computed based on the
        ///     X, Y, Width and Height properties and set on the Frame.
        /// </summary>
        Computed
    }
}