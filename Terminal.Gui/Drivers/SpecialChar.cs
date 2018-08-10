namespace Terminal.Gui.Drivers
{
    /// <summary>
    ///     Special characters that can be drawn with Driver.AddSpecial.
    /// </summary>
    public enum SpecialChar
    {
        /// <summary>
        ///     Horizontal line character.
        /// </summary>
        HLine,

        /// <summary>
        ///     Vertical line character.
        /// </summary>
        VLine,

        /// <summary>
        ///     Stipple pattern
        /// </summary>
        Stipple,

        /// <summary>
        ///     Diamond character
        /// </summary>
        Diamond,

        /// <summary>
        ///     Upper left corner
        /// </summary>
        ULCorner,

        /// <summary>
        ///     Lower left corner
        /// </summary>
        LLCorner,

        /// <summary>
        ///     Upper right corner
        /// </summary>
        URCorner,

        /// <summary>
        ///     Lower right corner
        /// </summary>
        LRCorner,

        /// <summary>
        ///     Left tee
        /// </summary>
        LeftTee,

        /// <summary>
        ///     Right tee
        /// </summary>
        RightTee,

        /// <summary>
        ///     Top tee
        /// </summary>
        TopTee,

        /// <summary>
        ///     The bottom tee.
        /// </summary>
        BottomTee
    }
}