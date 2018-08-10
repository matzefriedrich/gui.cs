namespace Terminal.Gui
{
    /// <summary>
    ///     The Key enumeration contains special encoding for some keys, but can also
    ///     encode all the unicode values that can be passed.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         If the SpecialMask is set, then the value is that of the special mask,
    ///         otherwise, the value is the one of the lower bits (as extracted by CharMask)
    ///     </para>
    ///     <para>
    ///         Control keys are the values between 1 and 26 corresponding to Control-A to Control-Z
    ///     </para>
    ///     <para>
    ///         Unicode runes are also stored here, the letter 'A" for example is encoded as a value 65 (not surfaced in the
    ///         enum).
    ///     </para>
    /// </remarks>
    public enum Key : uint
    {
        /// <summary>
        ///     Mask that indictes that this is a character value, values outside this range
        ///     indicate special characters like Alt-key combinations or special keys on the
        ///     keyboard like function keys, arrows keys and so on.
        /// </summary>
        CharMask = 0xfffff,

        /// <summary>
        ///     If the SpecialMask is set, then the value is that of the special mask,
        ///     otherwise, the value is the one of the lower bits (as extracted by CharMask).
        /// </summary>
        SpecialMask = 0xfff00000,

        /// <summary>
        ///     The key code for the user pressing Control-spacebar
        /// </summary>
        ControlSpace = 0,

        /// <summary>
        ///     The key code for the user pressing Control-A
        /// </summary>
        ControlA = 1,

        /// <summary>
        ///     The key code for the user pressing Control-B
        /// </summary>
        ControlB,

        /// <summary>
        ///     The key code for the user pressing Control-C
        /// </summary>
        ControlC,

        /// <summary>
        ///     The key code for the user pressing Control-D
        /// </summary>
        ControlD,

        /// <summary>
        ///     The key code for the user pressing Control-E
        /// </summary>
        ControlE,

        /// <summary>
        ///     The key code for the user pressing Control-F
        /// </summary>
        ControlF,

        /// <summary>
        ///     The key code for the user pressing Control-G
        /// </summary>
        ControlG,

        /// <summary>
        ///     The key code for the user pressing Control-H
        /// </summary>
        ControlH,

        /// <summary>
        ///     The key code for the user pressing Control-I (same as the tab key).
        /// </summary>
        ControlI,

        /// <summary>
        ///     The key code for the user pressing the tab key (same as pressing Control-I).
        /// </summary>
        Tab = ControlI,

        /// <summary>
        ///     The key code for the user pressing Control-J
        /// </summary>
        ControlJ,

        /// <summary>
        ///     The key code for the user pressing Control-K
        /// </summary>
        ControlK,

        /// <summary>
        ///     The key code for the user pressing Control-L
        /// </summary>
        ControlL,

        /// <summary>
        ///     The key code for the user pressing Control-M
        /// </summary>
        ControlM,

        /// <summary>
        ///     The key code for the user pressing Control-N (same as the return key).
        /// </summary>
        ControlN,

        /// <summary>
        ///     The key code for the user pressing Control-O
        /// </summary>
        ControlO,

        /// <summary>
        ///     The key code for the user pressing Control-P
        /// </summary>
        ControlP,

        /// <summary>
        ///     The key code for the user pressing Control-Q
        /// </summary>
        ControlQ,

        /// <summary>
        ///     The key code for the user pressing Control-R
        /// </summary>
        ControlR,

        /// <summary>
        ///     The key code for the user pressing Control-S
        /// </summary>
        ControlS,

        /// <summary>
        ///     The key code for the user pressing Control-T
        /// </summary>
        ControlT,

        /// <summary>
        ///     The key code for the user pressing Control-U
        /// </summary>
        ControlU,

        /// <summary>
        ///     The key code for the user pressing Control-V
        /// </summary>
        ControlV,

        /// <summary>
        ///     The key code for the user pressing Control-W
        /// </summary>
        ControlW,

        /// <summary>
        ///     The key code for the user pressing Control-X
        /// </summary>
        ControlX,

        /// <summary>
        ///     The key code for the user pressing Control-Y
        /// </summary>
        ControlY,

        /// <summary>
        ///     The key code for the user pressing Control-Z
        /// </summary>
        ControlZ,

        /// <summary>
        ///     The key code for the user pressing the escape key
        /// </summary>
        Esc = 27,

        /// <summary>
        ///     The key code for the user pressing the return key.
        /// </summary>
        Enter = '\n',

        /// <summary>
        ///     The key code for the user pressing the space bar
        /// </summary>
        Space = 32,

        /// <summary>
        ///     The key code for the user pressing the delete key.
        /// </summary>
        Delete = 127,

        /// <summary>
        ///     When this value is set, the Key encodes the sequence Alt-KeyValue.
        ///     And the actual value must be extracted by removing the AltMask.
        /// </summary>
        AltMask = 0x80000000,

        /// <summary>
        ///     Backspace key.
        /// </summary>
        Backspace = 0x100000,

        /// <summary>
        ///     Cursor up key
        /// </summary>
        CursorUp,

        /// <summary>
        ///     Cursor down key.
        /// </summary>
        CursorDown,

        /// <summary>
        ///     Cursor left key.
        /// </summary>
        CursorLeft,

        /// <summary>
        ///     Cursor right key.
        /// </summary>
        CursorRight,

        /// <summary>
        ///     Page Up key.
        /// </summary>
        PageUp,

        /// <summary>
        ///     Page Down key.
        /// </summary>
        PageDown,

        /// <summary>
        ///     Home key
        /// </summary>
        Home,

        /// <summary>
        ///     End key
        /// </summary>
        End,

        /// <summary>
        ///     Delete character key
        /// </summary>
        DeleteChar,

        /// <summary>
        ///     Insert character key
        /// </summary>
        InsertChar,

        /// <summary>
        ///     F1 key.
        /// </summary>
        F1,

        /// <summary>
        ///     F2 key.
        /// </summary>
        F2,

        /// <summary>
        ///     F3 key.
        /// </summary>
        F3,

        /// <summary>
        ///     F4 key.
        /// </summary>
        F4,

        /// <summary>
        ///     F5 key.
        /// </summary>
        F5,

        /// <summary>
        ///     F6 key.
        /// </summary>
        F6,

        /// <summary>
        ///     F7 key.
        /// </summary>
        F7,

        /// <summary>
        ///     F8 key.
        /// </summary>
        F8,

        /// <summary>
        ///     F9 key.
        /// </summary>
        F9,

        /// <summary>
        ///     F10 key.
        /// </summary>
        F10,

        /// <summary>
        ///     Shift-tab key (backwards tab key).
        /// </summary>
        BackTab,

        /// <summary>
        ///     A key with an unknown mapping was raised.
        /// </summary>
        Unknown
    }
}