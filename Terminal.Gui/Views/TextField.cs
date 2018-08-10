//
// TextField.cs: single-line text editor with Emacs keybindings
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//

namespace Terminal.Gui
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using NStack;

    /// <summary>
    ///     Text data entry widget
    /// </summary>
    /// <remarks>
    ///     The Entry widget provides Emacs-like editing
    ///     functionality,  and mouse support.
    /// </remarks>
    public class TextField : View
    {
        private int first;

        private List<Rune> text;

        private bool used;

        /// <summary>
        ///     Public constructor that creates a text field, with layout controlled with X, Y, Width and Height.
        /// </summary>
        /// <param name="text">Initial text contents.</param>
        public TextField(string text) : this(ustring.Make(text))
        {
        }

        /// <summary>
        ///     Public constructor that creates a text field, with layout controlled with X, Y, Width and Height.
        /// </summary>
        /// <param name="text">Initial text contents.</param>
        public TextField(ustring text)
        {
            if (text == null)
                text = "";

            this.text = TextModel.ToRunes(text);
            this.CursorPosition = text.Length;
            this.CanFocus = true;
        }

        /// <summary>
        ///     Public constructor that creates a text field at an absolute position and size.
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="w">The width.</param>
        /// <param name="text">Initial text contents.</param>
        public TextField(int x, int y, int w, ustring text) : base(new Rect(x, y, w, 1))
        {
            if (text == null)
                text = "";

            this.text = TextModel.ToRunes(text);
            this.CursorPosition = text.Length;
            this.first = this.CursorPosition > w ? this.CursorPosition - w : 0;
            this.CanFocus = true;
        }

        public override Rect Frame
        {
            get => base.Frame;
            set
            {
                base.Frame = value;
                int w = base.Frame.Width;
                this.first = this.CursorPosition > w ? this.CursorPosition - w : 0;
            }
        }

        /// <summary>
        ///     Sets or gets the text in the entry.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public ustring Text
        {
            get => ustring.Make(this.text);

            set
            {
                this.text = TextModel.ToRunes(value);
                if (this.CursorPosition > this.text.Count)
                    this.CursorPosition = this.text.Count;

                // FIXME: this needs to be updated to use Rune.ColumnWidth
                this.first = this.CursorPosition > this.Frame.Width ? this.CursorPosition - this.Frame.Width : 0;
                this.SetNeedsDisplay();
            }
        }

        /// <summary>
        ///     Sets the secret property.
        /// </summary>
        /// <remarks>
        ///     This makes the text entry suitable for entering passwords.
        /// </remarks>
        public bool Secret { get; set; }

        /// <summary>
        ///     The current cursor position.
        /// </summary>
        public int CursorPosition { get; private set; }

        public override bool CanFocus
        {
            get => true;
            set => base.CanFocus = value;
        }

        /// <summary>
        ///     Changed event, raised when the text has clicked.
        /// </summary>
        /// <remarks>
        ///     Client code can hook up to this event, it is
        ///     raised when the text in the entry changes.
        /// </remarks>
        public event EventHandler Changed;

        /// <summary>
        ///     Sets the cursor position.
        /// </summary>
        public override void PositionCursor()
        {
            var col = 0;
            for (int idx = this.first; idx < this.text.Count; idx++)
            {
                if (idx == this.CursorPosition)
                    break;
                int cols = Rune.ColumnWidth(this.text[idx]);
                col += cols;
            }

            this.Move(col, 0);
        }

        public override void Redraw(Rect region)
        {
            Driver.SetAttribute(this.ColorScheme.Focus);
            this.Move(0, 0);

            int p = this.first;
            var col = 0;
            int width = this.Frame.Width;
            int tcount = this.text.Count;
            for (var idx = 0; idx < tcount; idx++)
            {
                Rune rune = this.text[idx];
                if (idx < this.first)
                    continue;
                int cols = Rune.ColumnWidth(rune);
                if (col + cols < width)
                    Driver.AddRune(this.Secret ? '*' : rune);
                col += cols;
            }

            for (int i = col; i < this.Frame.Width; i++)
                Driver.AddRune(' ');

            this.PositionCursor();
        }

        // Returns the size of the string starting at position start
        private int DisplaySize(List<Rune> t, int start)
        {
            var size = 0;
            int tcount = this.text.Count;
            for (int i = start; i < tcount; i++)
            {
                Rune rune = this.text[i];
                size += Rune.ColumnWidth(rune);
            }

            return size;
        }

        private void Adjust()
        {
            if (this.CursorPosition < this.first)
                this.first = this.CursorPosition;
            else if (this.first + this.CursorPosition >= this.Frame.Width)
                this.first = this.CursorPosition - (this.Frame.Width - 1);
            this.SetNeedsDisplay();
        }

        private void SetText(List<Rune> newText)
        {
            this.text = newText;
            if (this.Changed != null)
                this.Changed(this, EventArgs.Empty);
        }

        private void SetText(IEnumerable<Rune> newText)
        {
            this.SetText(newText.ToList());
        }

        private void SetClipboard(IEnumerable<Rune> text)
        {
            if (!this.Secret)
                Clipboard.Contents = ustring.Make(text.ToList());
        }

        public override bool ProcessKey(KeyEvent kb)
        {
            switch (kb.Key)
            {
                case Key.DeleteChar:
                case Key.ControlD:
                    if (this.text.Count == 0 || this.text.Count == this.CursorPosition)
                        return true;

                    this.SetText(this.text.GetRange(0, this.CursorPosition).Concat(this.text.GetRange(this.CursorPosition + 1, this.text.Count - (this.CursorPosition + 1))));
                    this.Adjust();
                    break;

                case Key.Delete:
                case Key.Backspace:
                    if (this.CursorPosition == 0)
                        return true;

                    this.SetText(this.text.GetRange(0, this.CursorPosition - 1).Concat(this.text.GetRange(this.CursorPosition, this.text.Count - this.CursorPosition)));
                    this.CursorPosition--;
                    this.Adjust();
                    break;

                // Home, C-A
                case Key.Home:
                case Key.ControlA:
                    this.CursorPosition = 0;
                    this.Adjust();
                    break;

                case Key.CursorLeft:
                case Key.ControlB:
                    if (this.CursorPosition > 0)
                    {
                        this.CursorPosition--;
                        this.Adjust();
                    }

                    break;

                case Key.End:
                case Key.ControlE: // End
                    this.CursorPosition = this.text.Count;
                    this.Adjust();
                    break;

                case Key.CursorRight:
                case Key.ControlF:
                    if (this.CursorPosition == this.text.Count)
                        break;
                    this.CursorPosition++;
                    this.Adjust();
                    break;

                case Key.ControlK: // kill-to-end
                    if (this.CursorPosition >= this.text.Count)
                        return true;
                    this.SetClipboard(this.text.GetRange(this.CursorPosition, this.text.Count - this.CursorPosition));
                    this.SetText(this.text.GetRange(0, this.CursorPosition));
                    this.Adjust();
                    break;

                case Key.ControlY: // Control-y, yank
                    List<Rune> clip = TextModel.ToRunes(Clipboard.Contents);
                    if (clip == null)
                        return true;

                    if (this.CursorPosition == this.text.Count)
                    {
                        this.SetText(this.text.Concat(clip).ToList());
                        this.CursorPosition = this.text.Count;
                    }
                    else
                    {
                        this.SetText(this.text.GetRange(0, this.CursorPosition).Concat(clip).Concat(this.text.GetRange(this.CursorPosition, this.text.Count - this.CursorPosition)));
                        this.CursorPosition += clip.Count;
                    }

                    this.Adjust();
                    break;

                case 'b' + Key.AltMask:
                    int bw = this.WordBackward(this.CursorPosition);
                    if (bw != -1)
                        this.CursorPosition = bw;
                    this.Adjust();
                    break;

                case 'f' + Key.AltMask:
                    int fw = this.WordForward(this.CursorPosition);
                    if (fw != -1)
                        this.CursorPosition = fw;
                    this.Adjust();
                    break;

                // MISSING:
                // Alt-D, Alt-backspace
                // Alt-Y
                // Delete adding to kill buffer

                default:
                    // Ignore other control characters.
                    if (kb.Key < Key.Space || kb.Key > Key.CharMask)
                        return false;

                    List<Rune> kbstr = TextModel.ToRunes(ustring.Make((uint) kb.Key));
                    if (this.used)
                    {
                        if (this.CursorPosition == this.text.Count)
                            this.SetText(this.text.Concat(kbstr).ToList());
                        else
                            this.SetText(this.text.GetRange(0, this.CursorPosition).Concat(kbstr).Concat(this.text.GetRange(this.CursorPosition, this.text.Count - this.CursorPosition)));
                        this.CursorPosition++;
                    }
                    else
                    {
                        this.SetText(kbstr);
                        this.first = 0;
                        this.CursorPosition = 1;
                    }

                    this.used = true;
                    this.Adjust();
                    return true;
            }

            this.used = true;
            return true;
        }

        private int WordForward(int p)
        {
            if (p >= this.text.Count)
                return -1;

            int i = p;
            if (Rune.IsPunctuation(this.text[p]) || Rune.IsWhiteSpace(this.text[p]))
            {
                for (; i < this.text.Count; i++)
                {
                    Rune r = this.text[i];
                    if (Rune.IsLetterOrDigit(r))
                        break;
                }

                for (; i < this.text.Count; i++)
                {
                    Rune r = this.text[i];
                    if (!Rune.IsLetterOrDigit(r))
                        break;
                }
            }
            else
            {
                for (; i < this.text.Count; i++)
                {
                    Rune r = this.text[i];
                    if (!Rune.IsLetterOrDigit(r))
                        break;
                }
            }

            if (i != p)
                return i;
            return -1;
        }

        private int WordBackward(int p)
        {
            if (p == 0)
                return -1;

            int i = p - 1;
            if (i == 0)
                return 0;

            Rune ti = this.text[i];
            if (Rune.IsPunctuation(ti) || Rune.IsSymbol(ti) || Rune.IsWhiteSpace(ti))
            {
                for (; i >= 0; i--)
                    if (Rune.IsLetterOrDigit(this.text[i]))
                        break;
                for (; i >= 0; i--)
                    if (!Rune.IsLetterOrDigit(this.text[i]))
                        break;
            }
            else
            {
                for (; i >= 0; i--)
                    if (!Rune.IsLetterOrDigit(this.text[i]))
                        break;
            }

            i++;

            if (i != p)
                return i;

            return -1;
        }

        public override bool MouseEvent(MouseEvent ev)
        {
            if (!ev.Flags.HasFlag(MouseFlags.Button1Clicked))
                return false;

            if (!this.HasFocus)
                this.SuperView.SetFocus(this);

            // We could also set the cursor position.
            this.CursorPosition = this.first + ev.X;
            if (this.CursorPosition > this.text.Count)
                this.CursorPosition = this.text.Count;
            if (this.CursorPosition < this.first)
                this.CursorPosition = 0;

            this.SetNeedsDisplay();
            return true;
        }
    }
}