namespace Terminal.Gui
{
    /// <summary>
    ///     Color scheme definitions, they cover some common scenarios and are used
    ///     typically in toplevel containers to set the scheme that is used by all the
    ///     views contained inside.
    /// </summary>
    public class ColorScheme
    {
        /// <summary>
        ///     The color for text when the view has the focus.
        /// </summary>
        public Attribute Focus;

        /// <summary>
        ///     The color for the hotkey when the view is focused.
        /// </summary>
        public Attribute HotFocus;

        /// <summary>
        ///     The color for the hotkey when a view is not focused
        /// </summary>
        public Attribute HotNormal;

        /// <summary>
        ///     The default color for text, when the view is not focused.
        /// </summary>
        public Attribute Normal;
    }
}