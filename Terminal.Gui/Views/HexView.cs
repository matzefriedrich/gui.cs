//
// HexView.cs: A hexadecimal viewer
//
// TODO:
// - Support searching and highlighting of the search result
// - Bug showing the last line
// 

namespace Terminal.Gui.Views
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using Terminal.Gui.Types;

    using Attribute = Terminal.Gui.Drivers.Attribute;

    /// <summary>
    ///     An Hex viewer an editor view over a System.IO.Stream
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This provides a hex editor on top of a seekable stream with the left side showing an hex
    ///         dump of the values in the stream and the right side showing the contents (filterd to
    ///         non-control sequence ascii characters).
    ///     </para>
    ///     <para>
    ///         Users can switch from one side to the other by using the tab key.
    ///     </para>
    ///     <para>
    ///         If you want to enable editing, set the AllowsEdits property, once that is done, the user
    ///         can make changes to the hexadecimal values of the stream.   Any changes done are tracked
    ///         in the Edits property which is a sorted dictionary indicating the position where the
    ///         change was made and the new value.    A convenience ApplyEdits method can be used to c
    ///         apply the methods to the underlying stream.
    ///     </para>
    ///     <para>
    ///         It is possible to control the first byte shown by setting the DisplayStart property
    ///         to the offset that you want to start viewing.
    ///     </para>
    /// </remarks>
    public class HexView : View
    {
        private const int displayWidth = 9;

        private const int bsize = 4;

        private int bytesPerLine;

        private long displayStart, position;

        private SortedDictionary<long, byte> edits = new SortedDictionary<long, byte>();

        private bool firstNibble, leftSide;

        private Stream source;

        /// <summary>
        ///     Creates and instance of the HexView that will render a seekable stream in hex on the allocated view region.
        /// </summary>
        /// <param name="source">Source stream, this stream should support seeking, or this will raise an exceotion.</param>
        public HexView(Stream source)
        {
            this.Source = source;
            this.source = source;
            this.CanFocus = true;
            this.leftSide = true;
            this.firstNibble = true;
        }

        /// <summary>
        ///     The source stream to display on the hex view, the stream should support seeking.
        /// </summary>
        /// <value>The source.</value>
        public Stream Source
        {
            get => this.source;
            set
            {
                if (value == null)
                    throw new ArgumentNullException("source");
                if (!value.CanSeek)
                    throw new ArgumentException("The source stream must be seekable (CanSeek property)", "source");
                this.source = value;

                this.SetNeedsDisplay();
            }
        }

        /// <summary>
        ///     Configures the initial offset to be displayed at the top
        /// </summary>
        /// <value>The display start.</value>
        public long DisplayStart
        {
            get => this.displayStart;
            set
            {
                this.position = value;

                this.SetDisplayStart(value);
            }
        }

        public override Rect Frame
        {
            get => base.Frame;
            set
            {
                base.Frame = value;

                // Small buffers will just show the position, with 4 bytes
                this.bytesPerLine = 4;
                if (value.Width - displayWidth > 17)
                    this.bytesPerLine = 4 * ((value.Width - displayWidth) / 18);
            }
        }

        /// <summary>
        ///     Gets or sets a value indicating whether this <see cref="T:Terminal.Gui.Views.HexView" /> allow editing of the contents of
        ///     the underlying stream.
        /// </summary>
        /// <value><c>true</c> if allow edits; otherwise, <c>false</c>.</value>
        public bool AllowEdits { get; set; }

        /// <summary>
        ///     Gets a list of the edits done to the buffer which is a sorted dictionary with the positions where the edit took
        ///     place and the value that was set.
        /// </summary>
        /// <value>The edits.</value>
        public IReadOnlyDictionary<long, byte> Edits => this.edits;

        internal void SetDisplayStart(long value)
        {
            if (value >= this.source.Length)
                this.displayStart = this.source.Length - 1;
            else if (value < 0)
                this.displayStart = 0;
            else
                this.displayStart = value;
            this.SetNeedsDisplay();
        }

        //
        // This is used to support editing of the buffer on a peer List<>, 
        // the offset corresponds to an offset relative to DisplayStart, and
        // the buffer contains the contents of a screenful of data, so the 
        // offset is relative to the buffer.
        //
        // 
        private byte GetData(byte[] buffer, int offset, out bool edited)
        {
            long pos = this.DisplayStart + offset;
            if (this.edits.TryGetValue(pos, out byte v))
            {
                edited = true;
                return v;
            }

            edited = false;
            return buffer[offset];
        }

        public override void Redraw(Rect region)
        {
            Attribute currentAttribute;
            Attribute current = this.ColorScheme.Focus;
            Driver.SetAttribute(current);
            this.Move(0, 0);

            Rect frame = this.Frame;

            int nblocks = this.bytesPerLine / 4;
            var data = new byte [nblocks * 4 * frame.Height];
            this.Source.Position = this.displayStart;
            int n = this.source.Read(data, 0, data.Length);

            int activeColor = this.ColorScheme.HotNormal;
            int trackingColor = this.ColorScheme.HotFocus;

            for (var line = 0; line < frame.Height; line++)
            {
                var lineRect = new Rect(0, line, frame.Width, 1);
                if (!region.Contains(lineRect))
                    continue;

                this.Move(0, line);
                Driver.SetAttribute(this.ColorScheme.HotNormal);
                Driver.AddStr(string.Format("{0:x8} ", this.displayStart + line * nblocks * 4));

                currentAttribute = this.ColorScheme.HotNormal;
                SetAttribute(this.ColorScheme.Normal);

                for (var block = 0; block < nblocks; block++)
                {
                    for (var b = 0; b < 4; b++)
                    {
                        int offset = line * nblocks * 4 + block * 4 + b;
                        bool edited;
                        byte value = this.GetData(data, offset, out edited);
                        if (offset + this.displayStart == this.position || edited)
                            SetAttribute(this.leftSide ? activeColor : trackingColor);
                        else
                            SetAttribute(this.ColorScheme.Normal);

                        Driver.AddStr(offset >= n ? "  " : string.Format("{0:x2}", value));
                        SetAttribute(this.ColorScheme.Normal);
                        Driver.AddRune(' ');
                    }

                    Driver.AddStr(block + 1 == nblocks ? " " : "| ");
                }


                for (var bitem = 0; bitem < nblocks * 4; bitem++)
                {
                    int offset = line * nblocks * 4 + bitem;

                    var edited = false;
                    Rune c = ' ';
                    if (offset >= n)
                    {
                        c = ' ';
                    }
                    else
                    {
                        byte b = this.GetData(data, offset, out edited);
                        if (b < 32)
                            c = '.';
                        else if (b > 127)
                            c = '.';
                        else
                            c = b;
                    }

                    if (offset + this.displayStart == this.position || edited)
                        SetAttribute(this.leftSide ? trackingColor : activeColor);
                    else
                        SetAttribute(this.ColorScheme.Normal);

                    Driver.AddRune(c);
                }
            }

            void SetAttribute(Attribute attribute)
            {
                if (currentAttribute != attribute)
                {
                    currentAttribute = attribute;
                    Driver.SetAttribute(attribute);
                }
            }
        }

        /// <summary>
        ///     Positions the cursor based for the hex view
        /// </summary>
        public override void PositionCursor()
        {
            var delta = (int) (this.position - this.displayStart);
            int line = delta / this.bytesPerLine;
            int item = delta % this.bytesPerLine;
            int block = item / 4;
            int column = item % 4 * 3;

            if (this.leftSide)
                this.Move(displayWidth + block * 14 + column + (this.firstNibble ? 0 : 1), line);
            else
                this.Move(displayWidth + this.bytesPerLine / 4 * 14 + item - 1, line);
        }

        private void RedisplayLine(long pos)
        {
            var delta = (int) (pos - this.DisplayStart);
            int line = delta / this.bytesPerLine;

            this.SetNeedsDisplay(new Rect(0, line, this.Frame.Width, 1));
        }

        private void CursorRight()
        {
            this.RedisplayLine(this.position);
            if (this.leftSide)
            {
                if (this.firstNibble)
                {
                    this.firstNibble = false;
                    return;
                }

                this.firstNibble = true;
            }

            if (this.position < this.source.Length)
                this.position++;
            if (this.position >= this.DisplayStart + this.bytesPerLine * this.Frame.Height)
            {
                this.SetDisplayStart(this.DisplayStart + this.bytesPerLine);
                this.SetNeedsDisplay();
            }
            else
            {
                this.RedisplayLine(this.position);
            }
        }

        private void MoveUp(int bytes)
        {
            this.RedisplayLine(this.position);
            this.position -= bytes;
            if (this.position < 0)
                this.position = 0;
            if (this.position < this.DisplayStart)
            {
                this.SetDisplayStart(this.DisplayStart - bytes);
                this.SetNeedsDisplay();
            }
            else
            {
                this.RedisplayLine(this.position);
            }
        }

        private void MoveDown(int bytes)
        {
            this.RedisplayLine(this.position);
            if (this.position + bytes < this.source.Length)
                this.position += bytes;
            if (this.position >= this.DisplayStart + this.bytesPerLine * this.Frame.Height)
            {
                this.SetDisplayStart(this.DisplayStart + bytes);
                this.SetNeedsDisplay();
            }
            else
            {
                this.RedisplayLine(this.position);
            }
        }

        public override bool ProcessKey(KeyEvent keyEvent)
        {
            switch (keyEvent.Key)
            {
                case Key.CursorLeft:
                    this.RedisplayLine(this.position);
                    if (this.leftSide)
                    {
                        if (!this.firstNibble)
                        {
                            this.firstNibble = true;
                            return true;
                        }

                        this.firstNibble = false;
                    }

                    if (this.position == 0)
                        return true;
                    if (this.position - 1 < this.DisplayStart)
                    {
                        this.SetDisplayStart(this.displayStart - this.bytesPerLine);
                        this.SetNeedsDisplay();
                    }
                    else
                    {
                        this.RedisplayLine(this.position);
                    }

                    this.position--;
                    break;
                case Key.CursorRight:
                    this.CursorRight();
                    break;
                case Key.CursorDown:
                    this.MoveDown(this.bytesPerLine);
                    break;
                case Key.CursorUp:
                    this.MoveUp(this.bytesPerLine);
                    break;
                case Key.Tab:
                    this.leftSide = !this.leftSide;
                    this.RedisplayLine(this.position);
                    this.firstNibble = true;
                    break;
                case 'v' + Key.AltMask:
                case Key.PageUp:
                    this.MoveUp(this.bytesPerLine * this.Frame.Height);
                    break;
                case Key.ControlV:
                case Key.PageDown:
                    this.MoveDown(this.bytesPerLine * this.Frame.Height);
                    break;
                case Key.Home:
                    this.DisplayStart = 0;
                    this.SetNeedsDisplay();
                    break;
                default:
                    if (this.leftSide)
                    {
                        int value = -1;
                        var k = (char) keyEvent.Key;
                        if (k >= 'A' && k <= 'F')
                            value = k - 'A' + 10;
                        else if (k >= 'a' && k <= 'f')
                            value = k - 'a' + 10;
                        else if (k >= '0' && k <= '9')
                            value = k - '0';
                        else
                            return false;

                        byte b;
                        if (!this.edits.TryGetValue(this.position, out b))
                        {
                            this.source.Position = this.position;
                            b = (byte) this.source.ReadByte();
                        }

                        this.RedisplayLine(this.position);
                        if (this.firstNibble)
                        {
                            this.firstNibble = false;
                            b = (byte) ((b & 0xf) | (value << 4));
                            this.edits[this.position] = b;
                        }
                        else
                        {
                            b = (byte) ((b & 0xf0) | value);
                            this.edits[this.position] = b;
                            this.CursorRight();
                        }

                        return true;
                    }
                    else
                    {
                        return false;
                    }
            }

            this.PositionCursor();
            return true;
        }

        /// <summary>
        ///     This method applies the edits to the stream and resets the contents of the Edits property
        /// </summary>
        public void ApplyEdits()
        {
            foreach (KeyValuePair<long, byte> kv in this.edits)
            {
                this.source.Position = kv.Key;
                this.source.WriteByte(kv.Value);
            }

            this.edits = new SortedDictionary<long, byte>();
        }
    }
}