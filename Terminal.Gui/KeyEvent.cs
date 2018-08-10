namespace Terminal.Gui
{
    /// <summary>
    ///     Describes a keyboard event.
    /// </summary>
    public struct KeyEvent
    {
        /// <summary>
        ///     Symb olid definition for the key.
        /// </summary>
        public Key Key;

        /// <summary>
        ///     The key value cast to an integer, you will typicall use this for
        ///     extracting the Unicode rune value out of a key, when none of the
        ///     symbolic options are in use.
        /// </summary>
        public int KeyValue => (int) this.Key;

        /// <summary>
        ///     Gets a value indicating whether the Alt key was pressed (real or synthesized)
        /// </summary>
        /// <value><c>true</c> if is alternate; otherwise, <c>false</c>.</value>
        public bool IsAlt => (this.Key & Key.AltMask) != 0;

        /// <summary>
        ///     Determines whether the value is a control key
        /// </summary>
        /// <value><c>true</c> if is ctrl; otherwise, <c>false</c>.</value>
        public bool IsCtrl => (uint) this.Key >= 1 && (uint) this.Key <= 26;

        /// <summary>
        ///     Constructs a new KeyEvent from the provided Key value - can be a rune cast into a Key value
        /// </summary>
        public KeyEvent(Key k)
        {
            this.Key = k;
        }
    }
}