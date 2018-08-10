//
// Button.cs: Button control
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//

namespace Terminal.Gui
{
    using System;

    using NStack;

    /// <summary>
    ///     Button is a view that provides an item that invokes a callback when activated.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Provides a button that can be clicked, or pressed with
    ///         the enter key and processes hotkeys (the first uppercase
    ///         letter in the button becomes the hotkey).
    ///     </para>
    ///     <para>
    ///         If the button is configured as the default (IsDefault) the button
    ///         will respond to the return key is no other view processes it, and
    ///         turns this into a clicked event.
    ///     </para>
    /// </remarks>
    public class Button : View
    {
        /// <summary>
        ///     Clicked event, raised when the button is clicked.
        /// </summary>
        /// <remarks>
        ///     Client code can hook up to this event, it is
        ///     raised when the button is activated either with
        ///     the mouse or the keyboard.
        /// </remarks>
        public Action Clicked;

        private Rune hot_key;

        private int hot_pos = -1;

        private bool is_default;

        private ustring shown_text;

        private ustring text;

        /// <summary>
        ///     Public constructor, creates a button based on
        ///     the given text at position 0,0
        /// </summary>
        /// <remarks>
        ///     The size of the button is computed based on the
        ///     text length.   This button is not a default button.
        /// </remarks>
        /// <param name="text">The button's text</param>
        /// <param name="is_default">
        ///     If set, this makes the button the default button in the current view, which means that if the
        ///     user presses return on a view that does not handle return, it will be treated as if he had clicked on the button
        /// </param>
        public Button(ustring text, bool is_default = false)
        {
            this.CanFocus = true;
            this.IsDefault = is_default;
            this.Text = text;
            int w = text.Length + 4 + (is_default ? 2 : 0);
            this.Width = w;
            this.Height = 1;
            this.Frame = new Rect(0, 0, w, 1);
        }

        /// <summary>
        ///     Public constructor, creates a button based on
        ///     the given text at the given position.
        /// </summary>
        /// <remarks>
        ///     The size of the button is computed based on the
        ///     text length.   This button is not a default button.
        /// </remarks>
        /// <param name="x">X position where the button will be shown.</param>
        /// <param name="y">Y position where the button will be shown.</param>
        /// <param name="text">The button's text</param>
        public Button(int x, int y, ustring text) : this(x, y, text, false)
        {
        }

        /// <summary>
        ///     Public constructor, creates a button based on
        ///     the given text at the given position.
        /// </summary>
        /// <remarks>
        ///     If the value for is_default is true, a special
        ///     decoration is used, and the enter key on a
        ///     dialog would implicitly activate this button.
        /// </remarks>
        /// <param name="x">X position where the button will be shown.</param>
        /// <param name="y">Y position where the button will be shown.</param>
        /// <param name="text">The button's text</param>
        /// <param name="is_default">
        ///     If set, this makes the button the default button in the current view, which means that if the
        ///     user presses return on a view that does not handle return, it will be treated as if he had clicked on the button
        /// </param>
        public Button(int x, int y, ustring text, bool is_default)
            : base(new Rect(x, y, text.Length + 4 + (is_default ? 2 : 0), 1))
        {
            this.CanFocus = true;

            this.IsDefault = is_default;
            this.Text = text;
        }

        /// <summary>
        ///     Gets or sets a value indicating whether this <see cref="T:Terminal.Gui.Button" /> is the default action to activate
        ///     on return on a dialog.
        /// </summary>
        /// <value><c>true</c> if is default; otherwise, <c>false</c>.</value>
        public bool IsDefault
        {
            get => this.is_default;
            set
            {
                this.is_default = value;
                this.Update();
            }
        }

        /// <summary>
        ///     The text displayed by this widget.
        /// </summary>
        public ustring Text
        {
            get => this.text;

            set
            {
                this.text = value;
                this.Update();
            }
        }

        internal void Update()
        {
            if (this.IsDefault)
                this.shown_text = "[< " + this.text + " >]";
            else
                this.shown_text = "[ " + this.text + " ]";

            this.hot_pos = -1;
            this.hot_key = 0;
            var i = 0;
            foreach (Rune c in this.shown_text)
            {
                if (Rune.IsUpper(c))
                {
                    this.hot_key = c;
                    this.hot_pos = i;
                    break;
                }

                i++;
            }

            this.SetNeedsDisplay();
        }

        public override void Redraw(Rect region)
        {
            Driver.SetAttribute(this.HasFocus ? this.ColorScheme.Focus : this.ColorScheme.Normal);
            this.Move(0, 0);
            Driver.AddStr(this.shown_text);

            if (this.hot_pos != -1)
            {
                this.Move(this.hot_pos, 0);
                Driver.SetAttribute(this.HasFocus ? this.ColorScheme.HotFocus : this.ColorScheme.HotNormal);
                Driver.AddRune(this.hot_key);
            }
        }

        public override void PositionCursor()
        {
            this.Move(this.hot_pos, 0);
        }

        private bool CheckKey(KeyEvent key)
        {
            if (char.ToUpper((char) key.KeyValue) == this.hot_key)
            {
                this.SuperView.SetFocus(this);
                if (this.Clicked != null)
                    this.Clicked();
                return true;
            }

            return false;
        }

        public override bool ProcessHotKey(KeyEvent kb)
        {
            if (kb.IsAlt)
                return this.CheckKey(kb);

            return false;
        }

        public override bool ProcessColdKey(KeyEvent kb)
        {
            if (this.IsDefault && kb.KeyValue == '\n')
            {
                if (this.Clicked != null)
                    this.Clicked();
                return true;
            }

            return this.CheckKey(kb);
        }

        public override bool ProcessKey(KeyEvent kb)
        {
            int c = kb.KeyValue;
            if (c == '\n' || c == ' ' || Rune.ToUpper((Rune) c) == this.hot_key)
            {
                if (this.Clicked != null)
                    this.Clicked();
                return true;
            }

            return base.ProcessKey(kb);
        }

        public override bool MouseEvent(MouseEvent me)
        {
            if (me.Flags == MouseFlags.Button1Clicked)
            {
                this.SuperView.SetFocus(this);
                this.SetNeedsDisplay();

                if (this.Clicked != null)
                    this.Clicked();
                return true;
            }

            return false;
        }
    }
}