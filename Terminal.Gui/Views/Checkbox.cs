//
// Checkbox.cs: Checkbox control
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//

namespace Terminal.Gui.Views
{
    using System;

    using NStack;

    using Terminal.Gui.Types;

    /// <summary>
    ///     The Checkbox View shows an on/off toggle that the user can set
    /// </summary>
    public class CheckBox : View
    {
        private Rune hot_key;

        private int hot_pos = -1;

        private ustring text;

        /// <summary>
        ///     Public constructor, creates a CheckButton based on the given text, uses Computed layout and sets the height and
        ///     width.
        /// </summary>
        /// <param name="s">S.</param>
        /// <param name="is_checked">If set to <c>true</c> is checked.</param>
        public CheckBox(ustring s, bool is_checked = false)
        {
            this.Checked = is_checked;
            this.Text = s;
            this.CanFocus = true;
            this.Height = 1;
            this.Width = s.Length + 4;
        }

        /// <summary>
        ///     Public constructor, creates a CheckButton based on
        ///     the given text at an absolute position.
        /// </summary>
        /// <remarks>
        ///     The size of CheckButton is computed based on the
        ///     text length. This CheckButton is not toggled.
        /// </remarks>
        public CheckBox(int x, int y, ustring s) : this(x, y, s, false)
        {
        }

        /// <summary>
        ///     Public constructor, creates a CheckButton based on
        ///     the given text at the given position and a state.
        /// </summary>
        /// <remarks>
        ///     The size of CheckButton is computed based on the
        ///     text length.
        /// </remarks>
        public CheckBox(int x, int y, ustring s, bool is_checked) : base(new Rect(x, y, s.Length + 4, 1))
        {
            this.Checked = is_checked;
            this.Text = s;

            this.CanFocus = true;
        }

        /// <summary>
        ///     The state of the checkbox.
        /// </summary>
        public bool Checked { get; set; }

        /// <summary>
        ///     The text displayed by this widget.
        /// </summary>
        public ustring Text
        {
            get => this.text;

            set
            {
                this.text = value;

                var i = 0;
                this.hot_pos = -1;
                this.hot_key = (char) 0;
                foreach (Rune c in this.text)
                {
                    if (Rune.IsUpper(c))
                    {
                        this.hot_key = c;
                        this.hot_pos = i;
                        break;
                    }

                    i++;
                }
            }
        }

        /// <summary>
        ///     Toggled event, raised when the CheckButton is toggled.
        /// </summary>
        /// <remarks>
        ///     Client code can hook up to this event, it is
        ///     raised when the checkbutton is activated either with
        ///     the mouse or the keyboard.
        /// </remarks>
        public event EventHandler Toggled;

        public override void Redraw(Rect region)
        {
            Driver.SetAttribute(this.HasFocus ? this.ColorScheme.Focus : this.ColorScheme.Normal);
            this.Move(0, 0);
            Driver.AddStr(this.Checked ? "[x] " : "[ ] ");
            this.Move(4, 0);
            Driver.AddStr(this.Text);
            if (this.hot_pos != -1)
            {
                this.Move(4 + this.hot_pos, 0);
                Driver.SetAttribute(this.HasFocus ? this.ColorScheme.HotFocus : this.ColorScheme.HotNormal);
                Driver.AddRune(this.hot_key);
            }
        }

        public override void PositionCursor()
        {
            this.Move(1, 0);
        }

        public override bool ProcessKey(KeyEvent kb)
        {
            if (kb.KeyValue == ' ')
            {
                this.Checked = !this.Checked;

                if (this.Toggled != null)
                    this.Toggled(this, EventArgs.Empty);

                this.SetNeedsDisplay();
                return true;
            }

            return base.ProcessKey(kb);
        }

        public override bool MouseEvent(MouseEvent me)
        {
            if (!me.Flags.HasFlag(MouseFlags.Button1Clicked))
                return false;

            this.SuperView.SetFocus(this);
            this.Checked = !this.Checked;
            this.SetNeedsDisplay();

            if (this.Toggled != null)
                this.Toggled(this, EventArgs.Empty);
            return true;
        }
    }
}