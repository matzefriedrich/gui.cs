//
// TextView.cs: multi-line text editing
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
// 
// TODO:
// PageUp/PageDown	
// Attributed text on spans
// Replace insertion with Insert method
// String accumulation (Control-k, control-k is not preserving the last new line, see StringToRunes
// Alt-D, Alt-Backspace
// API to set the cursor position
// API to scroll to a particular place
// keybindings to go to top/bottom
// public API to insert, remove ranges
// Add word forward/word backwards commands
// Save buffer API
// Mouse
//
// Desirable:
//   Move all the text manipulation into the TextModel


namespace Terminal.Gui.Views
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using NStack;

    using Terminal.Gui.Types;

    /// <summary>
    ///     Multi-line text editing view
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The text view provides a multi-line text view.   Users interact
    ///         with it with the standard Emacs commands for movement or the arrow
    ///         keys.
    ///     </para>
    ///     <list type="table">
    ///         <listheader>
    ///             <term>Shortcut</term>
    ///             <description>Action performed</description>
    ///         </listheader>
    ///         <item>
    ///             <term>Left cursor, Control-b</term>
    ///             <description>
    ///                 Moves the editing point left.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <term>Right cursor, Control-f</term>
    ///             <description>
    ///                 Moves the editing point right.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <term>Alt-b</term>
    ///             <description>
    ///                 Moves one word back.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <term>Alt-f</term>
    ///             <description>
    ///                 Moves one word forward.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <term>Up cursor, Control-p</term>
    ///             <description>
    ///                 Moves the editing point one line up.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <term>Down cursor, Control-n</term>
    ///             <description>
    ///                 Moves the editing point one line down
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <term>Home key, Control-a</term>
    ///             <description>
    ///                 Moves the cursor to the beginning of the line.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <term>End key, Control-e</term>
    ///             <description>
    ///                 Moves the cursor to the end of the line.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <term>Delete, Control-d</term>
    ///             <description>
    ///                 Deletes the character in front of the cursor.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <term>Backspace</term>
    ///             <description>
    ///                 Deletes the character behind the cursor.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <term>Control-k</term>
    ///             <description>
    ///                 Deletes the text until the end of the line and replaces the kill buffer
    ///                 with the deleted text.   You can paste this text in a different place by
    ///                 using Control-y.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <term>Control-y</term>
    ///             <description>
    ///                 Pastes the content of the kill ring into the current position.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <term>Alt-d</term>
    ///             <description>
    ///                 Deletes the word above the cursor and adds it to the kill ring.  You
    ///                 can paste the contents of the kill ring with Control-y.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <term>Control-q</term>
    ///             <description>
    ///                 Quotes the next input character, to prevent the normal processing of
    ///                 key handling to take place.
    ///             </description>
    ///         </item>
    ///     </list>
    /// </remarks>
    public class TextView : View
    {
        private readonly TextModel model = new TextModel();

        private int topRow;

        private int leftColumn;

        private int selectionStartColumn, selectionStartRow;

        private bool selecting;
        //bool used;

#if false
/// <summary>
///   Changed event, raised when the text has clicked.
/// </summary>
/// <remarks>
///   Client code can hook up to this event, it is
///   raised when the text in the entry changes.
/// </remarks>
		public event EventHandler Changed;
#endif
        /// <summary>
        ///     Public constructor, creates a view on the specified area, with absolute position and size.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public TextView(Rect frame) : base(frame)
        {
            this.CanFocus = true;
        }

        /// <summary>
        ///     Public constructor, creates a view on the specified area, with dimensions controlled with the X, Y, Width and
        ///     Height properties.
        /// </summary>
        public TextView()
        {
            this.CanFocus = true;
        }

        private void ResetPosition()
        {
            this.topRow = this.leftColumn = this.CurrentRow = this.CurrentColumn = 0;
        }

        /// <summary>
        ///     Sets or gets the text in the entry.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public ustring Text
        {
            get => this.model.ToString();

            set
            {
                this.ResetPosition();
                this.model.LoadString(value);
                this.SetNeedsDisplay();
            }
        }

        /// <summary>
        ///     Loads the contents of the file into the TextView.
        /// </summary>
        /// <returns><c>true</c>, if file was loaded, <c>false</c> otherwise.</returns>
        /// <param name="path">Path to the file to load.</param>
        public bool LoadFile(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            this.ResetPosition();
            bool res = this.model.LoadFile(path);
            this.SetNeedsDisplay();
            return res;
        }

        /// <summary>
        ///     Loads the contents of the stream into the TextView.
        /// </summary>
        /// <returns><c>true</c>, if stream was loaded, <c>false</c> otherwise.</returns>
        /// <param name="stream">Stream to load the contents from.</param>
        public void LoadStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            this.ResetPosition();
            this.model.LoadStream(stream);
            this.SetNeedsDisplay();
        }

        /// <summary>
        ///     The current cursor row.
        /// </summary>
        public int CurrentRow { get; private set; }

        /// <summary>
        ///     Gets the cursor column.
        /// </summary>
        /// <value>The cursor column.</value>
        public int CurrentColumn { get; private set; }

        /// <summary>
        ///     Positions the cursor on the current row and column
        /// </summary>
        public override void PositionCursor()
        {
            if (this.selecting)
            {
                int minRow = Math.Min(Math.Max(Math.Min(this.selectionStartRow, this.CurrentRow) - this.topRow, 0), this.Frame.Height);
                int maxRow = Math.Min(Math.Max(Math.Max(this.selectionStartRow, this.CurrentRow) - this.topRow, 0), this.Frame.Height);

                this.SetNeedsDisplay(new Rect(0, minRow, this.Frame.Width, maxRow));
            }

            this.Move(this.CurrentColumn - this.leftColumn, this.CurrentRow - this.topRow);
        }

        private void ClearRegion(int left, int top, int right, int bottom)
        {
            for (int row = top; row < bottom; row++)
            {
                this.Move(left, row);
                for (int col = left; col < right; col++)
                    this.AddRune(col, row, ' ');
            }
        }

        private void ColorNormal()
        {
            Driver.SetAttribute(this.ColorScheme.Normal);
        }

        private void ColorSelection()
        {
            if (this.HasFocus)
                Driver.SetAttribute(this.ColorScheme.Focus);
            else
                Driver.SetAttribute(this.ColorScheme.Normal);
        }

        // Returns an encoded region start..end (top 32 bits are the row, low32 the column)
        private void GetEncodedRegionBounds(out long start, out long end)
        {
            long selection = ((long) (uint) this.selectionStartRow << 32) | (uint) this.selectionStartColumn;
            long point = ((long) (uint) this.CurrentRow << 32) | (uint) this.CurrentColumn;
            if (selection > point)
            {
                start = point;
                end = selection;
            }
            else
            {
                start = selection;
                end = point;
            }
        }

        private bool PointInSelection(int col, int row)
        {
            long start, end;
            this.GetEncodedRegionBounds(out start, out end);
            long q = ((long) (uint) row << 32) | (uint) col;
            return q >= start && q <= end;
        }

        //
        // Returns a ustring with the text in the selected 
        // region.
        //
        private ustring GetRegion()
        {
            long start, end;
            this.GetEncodedRegionBounds(out start, out end);
            var startRow = (int) (start >> 32);
            var maxrow = (int) (end >> 32);
            var startCol = (int) (start & 0xffffffff);
            var endCol = (int) (end & 0xffffffff);
            List<Rune> line = this.model.GetLine(startRow);

            if (startRow == maxrow)
                return this.StringFromRunes(line.GetRange(startCol, endCol));

            ustring res = this.StringFromRunes(line.GetRange(startCol, line.Count - startCol));

            for (int row = startRow + 1; row < maxrow; row++)
                res = res + ustring.Make((Rune) 10) + this.StringFromRunes(this.model.GetLine(row));
            line = this.model.GetLine(maxrow);
            res = res + ustring.Make((Rune) 10) + this.StringFromRunes(line.GetRange(0, endCol));
            return res;
        }

        //
        // Clears the contents of the selected region
        //
        private void ClearRegion()
        {
            long start, end;
            long currentEncoded = ((long) (uint) this.CurrentRow << 32) | (uint) this.CurrentColumn;
            this.GetEncodedRegionBounds(out start, out end);
            var startRow = (int) (start >> 32);
            var maxrow = (int) (end >> 32);
            var startCol = (int) (start & 0xffffffff);
            var endCol = (int) (end & 0xffffffff);
            List<Rune> line = this.model.GetLine(startRow);

            if (startRow == maxrow)
            {
                line.RemoveRange(startCol, endCol - startCol);
                this.CurrentColumn = startCol;
                this.SetNeedsDisplay(new Rect(0, startRow - this.topRow, this.Frame.Width, startRow - this.topRow + 1));
                return;
            }

            line.RemoveRange(startCol, line.Count - startCol);
            List<Rune> line2 = this.model.GetLine(maxrow);
            line.AddRange(line2.Skip(endCol));
            for (int row = startRow + 1; row <= maxrow; row++)
                this.model.RemoveLine(startRow + 1);
            if (currentEncoded == end)
                this.CurrentRow -= maxrow - startRow;
            this.CurrentColumn = startCol;

            this.SetNeedsDisplay();
        }

        /// <summary>
        ///     Redraw the text editor region
        /// </summary>
        /// <param name="region">The region to redraw.</param>
        public override void Redraw(Rect region)
        {
            this.ColorNormal();

            int bottom = region.Bottom;
            int right = region.Right;
            for (int row = region.Top; row < bottom; row++)
            {
                int textLine = this.topRow + row;
                if (textLine >= this.model.Count)
                {
                    this.ColorNormal();
                    this.ClearRegion(region.Left, row, region.Right, row + 1);
                    continue;
                }

                List<Rune> line = this.model.GetLine(textLine);
                int lineRuneCount = line.Count;
                if (line.Count < region.Left)
                {
                    this.ClearRegion(region.Left, row, region.Right, row + 1);
                    continue;
                }

                this.Move(region.Left, row);
                for (int col = region.Left; col < right; col++)
                {
                    int lineCol = this.leftColumn + col;
                    Rune rune = lineCol >= lineRuneCount ? ' ' : line[lineCol];
                    if (this.selecting && this.PointInSelection(col, row))
                        this.ColorSelection();
                    else
                        this.ColorNormal();

                    this.AddRune(col, row, rune);
                }
            }

            this.PositionCursor();
        }

        public override bool CanFocus
        {
            get => true;
            set => base.CanFocus = value;
        }

        private void SetClipboard(ustring text)
        {
            Clipboard.Contents = text;
        }

        private void AppendClipboard(ustring text)
        {
            Clipboard.Contents = Clipboard.Contents + text;
        }

        private void Insert(Rune rune)
        {
            List<Rune> line = this.GetCurrentLine();
            line.Insert(this.CurrentColumn, rune);
            int prow = this.CurrentRow - this.topRow;

            this.SetNeedsDisplay(new Rect(0, prow, this.Frame.Width, prow + 1));
        }

        private ustring StringFromRunes(List<Rune> runes)
        {
            if (runes == null)
                throw new ArgumentNullException(nameof(runes));
            var size = 0;
            foreach (Rune rune in runes)
                size += Utf8.RuneLen(rune);
            var encoded = new byte [size];
            var offset = 0;
            foreach (Rune rune in runes)
                offset += Utf8.EncodeRune(rune, encoded, offset);
            return ustring.Make(encoded);
        }

        private List<Rune> GetCurrentLine()
        {
            return this.model.GetLine(this.CurrentRow);
        }

        private void InsertText(ustring text)
        {
            List<List<Rune>> lines = TextModel.StringToRunes(text);

            if (lines.Count == 0)
                return;

            List<Rune> line = this.GetCurrentLine();

            // Optmize single line
            if (lines.Count == 1)
            {
                line.InsertRange(this.CurrentColumn, lines[0]);
                this.CurrentColumn += lines[0].Count;
                if (this.CurrentColumn - this.leftColumn > this.Frame.Width)
                    this.leftColumn = this.CurrentColumn - this.Frame.Width + 1;
                this.SetNeedsDisplay(new Rect(0, this.CurrentRow - this.topRow, this.Frame.Width, this.CurrentRow - this.topRow + 1));
                return;
            }

            // Keep a copy of the rest of the line
            int restCount = line.Count - this.CurrentColumn;
            List<Rune> rest = line.GetRange(this.CurrentColumn, restCount);
            line.RemoveRange(this.CurrentColumn, restCount);

            // First line is inserted at the current location, the rest is appended
            line.InsertRange(this.CurrentColumn, lines[0]);

            for (var i = 1; i < lines.Count; i++)
                this.model.AddLine(this.CurrentRow + i, lines[i]);

            List<Rune> last = this.model.GetLine(this.CurrentRow + lines.Count - 1);
            int lastp = last.Count;
            last.InsertRange(last.Count, rest);

            // Now adjjust column and row positions
            this.CurrentRow += lines.Count - 1;
            this.CurrentColumn = lastp;
            if (this.CurrentRow - this.topRow > this.Frame.Height)
            {
                this.topRow = this.CurrentRow - this.Frame.Height + 1;
                if (this.topRow < 0)
                    this.topRow = 0;
            }

            if (this.CurrentColumn < this.leftColumn)
                this.leftColumn = this.CurrentColumn;
            if (this.CurrentColumn - this.leftColumn >= this.Frame.Width)
                this.leftColumn = this.CurrentColumn - this.Frame.Width + 1;
            this.SetNeedsDisplay();
        }

        // The column we are tracking, or -1 if we are not tracking any column
        private int columnTrack = -1;

        // Tries to snap the cursor to the tracking column
        private void TrackColumn()
        {
            // Now track the column
            List<Rune> line = this.GetCurrentLine();
            if (line.Count < this.columnTrack)
                this.CurrentColumn = line.Count;
            else if (this.columnTrack != -1)
                this.CurrentColumn = this.columnTrack;
            else if (this.CurrentColumn > line.Count)
                this.CurrentColumn = line.Count;
            this.Adjust();
        }

        private void Adjust()
        {
            var need = false;
            if (this.CurrentColumn < this.leftColumn)
            {
                this.CurrentColumn = this.leftColumn;
                need = true;
            }

            if (this.CurrentColumn - this.leftColumn > this.Frame.Width)
            {
                this.leftColumn = this.CurrentColumn - this.Frame.Width + 1;
                need = true;
            }

            if (this.CurrentRow < this.topRow)
            {
                this.topRow = this.CurrentRow;
                need = true;
            }

            if (this.CurrentRow - this.topRow > this.Frame.Height)
            {
                this.topRow = this.CurrentRow - this.Frame.Height + 1;
                need = true;
            }

            if (need)
                this.SetNeedsDisplay();
            else
                this.PositionCursor();
        }

        private bool lastWasKill;

        public override bool ProcessKey(KeyEvent kb)
        {
            int restCount;
            List<Rune> rest;

            // Handle some state here - whether the last command was a kill
            // operation and the column tracking (up/down)
            switch (kb.Key)
            {
                case Key.ControlN:
                case Key.CursorDown:
                case Key.ControlP:
                case Key.CursorUp:
                    this.lastWasKill = false;
                    break;
                case Key.ControlK:
                    break;
                default:
                    this.lastWasKill = false;
                    this.columnTrack = -1;
                    break;
            }

            // Dispatch the command.
            switch (kb.Key)
            {
                case Key.ControlN:
                case Key.CursorDown:
                    if (this.CurrentRow + 1 < this.model.Count)
                    {
                        if (this.columnTrack == -1)
                            this.columnTrack = this.CurrentColumn;
                        this.CurrentRow++;
                        if (this.CurrentRow >= this.topRow + this.Frame.Height)
                        {
                            this.topRow++;
                            this.SetNeedsDisplay();
                        }

                        this.TrackColumn();
                        this.PositionCursor();
                    }

                    break;

                case Key.ControlP:
                case Key.CursorUp:
                    if (this.CurrentRow > 0)
                    {
                        if (this.columnTrack == -1)
                            this.columnTrack = this.CurrentColumn;
                        this.CurrentRow--;
                        if (this.CurrentRow < this.topRow)
                        {
                            this.topRow--;
                            this.SetNeedsDisplay();
                        }

                        this.TrackColumn();
                        this.PositionCursor();
                    }

                    break;

                case Key.ControlF:
                case Key.CursorRight:
                    List<Rune> currentLine = this.GetCurrentLine();
                    if (this.CurrentColumn < currentLine.Count)
                    {
                        this.CurrentColumn++;
                        if (this.CurrentColumn >= this.leftColumn + this.Frame.Width)
                        {
                            this.leftColumn++;
                            this.SetNeedsDisplay();
                        }

                        this.PositionCursor();
                    }
                    else
                    {
                        if (this.CurrentRow + 1 < this.model.Count)
                        {
                            this.CurrentRow++;
                            this.CurrentColumn = 0;
                            this.leftColumn = 0;
                            if (this.CurrentRow >= this.topRow + this.Frame.Height)
                                this.topRow++;
                            this.SetNeedsDisplay();
                            this.PositionCursor();
                        }
                    }

                    break;

                case Key.ControlB:
                case Key.CursorLeft:
                    if (this.CurrentColumn > 0)
                    {
                        this.CurrentColumn--;
                        if (this.CurrentColumn < this.leftColumn)
                        {
                            this.leftColumn--;
                            this.SetNeedsDisplay();
                        }

                        this.PositionCursor();
                    }
                    else
                    {
                        if (this.CurrentRow > 0)
                        {
                            this.CurrentRow--;
                            if (this.CurrentRow < this.topRow)
                                this.topRow--;
                            currentLine = this.GetCurrentLine();
                            this.CurrentColumn = currentLine.Count;
                            int prev = this.leftColumn;
                            this.leftColumn = this.CurrentColumn - this.Frame.Width + 1;
                            if (this.leftColumn < 0)
                                this.leftColumn = 0;
                            if (prev != this.leftColumn)
                                this.SetNeedsDisplay();
                            this.PositionCursor();
                        }
                    }

                    break;

                case Key.Delete:
                case Key.Backspace:
                    if (this.CurrentColumn > 0)
                    {
                        // Delete backwards 
                        currentLine = this.GetCurrentLine();
                        currentLine.RemoveAt(this.CurrentColumn - 1);
                        this.CurrentColumn--;
                        if (this.CurrentColumn < this.leftColumn)
                        {
                            this.leftColumn--;
                            this.SetNeedsDisplay();
                        }
                        else
                        {
                            this.SetNeedsDisplay(new Rect(0, this.CurrentRow - this.topRow, 1, this.Frame.Width));
                        }
                    }
                    else
                    {
                        // Merges the current line with the previous one.
                        if (this.CurrentRow == 0)
                            return true;
                        int prowIdx = this.CurrentRow - 1;
                        List<Rune> prevRow = this.model.GetLine(prowIdx);
                        int prevCount = prevRow.Count;
                        this.model.GetLine(prowIdx).AddRange(this.GetCurrentLine());
                        this.model.RemoveLine(this.CurrentRow);
                        this.CurrentRow--;
                        this.CurrentColumn = prevCount;
                        this.leftColumn = this.CurrentColumn - this.Frame.Width + 1;
                        if (this.leftColumn < 0)
                            this.leftColumn = 0;
                        this.SetNeedsDisplay();
                    }

                    break;

                // Home, C-A
                case Key.Home:
                case Key.ControlA:
                    this.CurrentColumn = 0;
                    if (this.CurrentColumn < this.leftColumn)
                    {
                        this.leftColumn = 0;
                        this.SetNeedsDisplay();
                    }
                    else
                    {
                        this.PositionCursor();
                    }

                    break;

                case Key.ControlD: // Delete
                    currentLine = this.GetCurrentLine();
                    if (this.CurrentColumn == currentLine.Count)
                    {
                        if (this.CurrentRow + 1 == this.model.Count)
                            break;
                        List<Rune> nextLine = this.model.GetLine(this.CurrentRow + 1);
                        currentLine.AddRange(nextLine);
                        this.model.RemoveLine(this.CurrentRow + 1);
                        int sr = this.CurrentRow - this.topRow;
                        this.SetNeedsDisplay(new Rect(0, sr, this.Frame.Width, sr + 1));
                    }
                    else
                    {
                        currentLine.RemoveAt(this.CurrentColumn);
                        int r = this.CurrentRow - this.topRow;
                        this.SetNeedsDisplay(new Rect(this.CurrentColumn - this.leftColumn, r, this.Frame.Width, r + 1));
                    }

                    break;

                case Key.End:
                case Key.ControlE: // End
                    currentLine = this.GetCurrentLine();
                    this.CurrentColumn = currentLine.Count;
                    int pcol = this.leftColumn;
                    this.leftColumn = this.CurrentColumn - this.Frame.Width + 1;
                    if (this.leftColumn < 0)
                        this.leftColumn = 0;
                    if (pcol != this.leftColumn)
                        this.SetNeedsDisplay();
                    this.PositionCursor();
                    break;

                case Key.ControlK: // kill-to-end
                    currentLine = this.GetCurrentLine();
                    if (currentLine.Count == 0)
                    {
                        this.model.RemoveLine(this.CurrentRow);
                        ustring val = ustring.Make((Rune) '\n');
                        if (this.lastWasKill)
                            this.AppendClipboard(val);
                        else
                            this.SetClipboard(val);
                    }
                    else
                    {
                        restCount = currentLine.Count - this.CurrentColumn;
                        rest = currentLine.GetRange(this.CurrentColumn, restCount);
                        ustring val = this.StringFromRunes(rest);
                        if (this.lastWasKill)
                            this.AppendClipboard(val);
                        else
                            this.SetClipboard(val);
                        currentLine.RemoveRange(this.CurrentColumn, restCount);
                    }

                    this.SetNeedsDisplay(new Rect(0, this.CurrentRow - this.topRow, this.Frame.Width, this.Frame.Height));
                    this.lastWasKill = true;
                    break;

                case Key.ControlY: // Control-y, yank
                    this.InsertText(Clipboard.Contents);
                    this.selecting = false;
                    break;

                case Key.ControlSpace:
                    this.selecting = true;
                    this.selectionStartColumn = this.CurrentColumn;
                    this.selectionStartRow = this.CurrentRow;
                    break;

                case 'w' + Key.AltMask:
                    this.SetClipboard(this.GetRegion());
                    this.selecting = false;
                    break;

                case Key.ControlW:
                    this.SetClipboard(this.GetRegion());
                    this.ClearRegion();
                    this.selecting = false;
                    break;

                case 'b' + Key.AltMask:
                    (int col, int row)? newPos = this.WordBackward(this.CurrentColumn, this.CurrentRow);
                    if (newPos.HasValue)
                    {
                        this.CurrentColumn = newPos.Value.col;
                        this.CurrentRow = newPos.Value.row;
                    }

                    this.Adjust();

                    break;

                case 'f' + Key.AltMask:
                    newPos = this.WordForward(this.CurrentColumn, this.CurrentRow);
                    if (newPos.HasValue)
                    {
                        this.CurrentColumn = newPos.Value.col;
                        this.CurrentRow = newPos.Value.row;
                    }

                    this.Adjust();
                    break;

                case Key.Enter:
                    int orow = this.CurrentRow;
                    currentLine = this.GetCurrentLine();
                    restCount = currentLine.Count - this.CurrentColumn;
                    rest = currentLine.GetRange(this.CurrentColumn, restCount);
                    currentLine.RemoveRange(this.CurrentColumn, restCount);
                    this.model.AddLine(this.CurrentRow + 1, rest);
                    this.CurrentRow++;
                    var fullNeedsDisplay = false;
                    if (this.CurrentRow >= this.topRow + this.Frame.Height)
                    {
                        this.topRow++;
                        fullNeedsDisplay = true;
                    }

                    this.CurrentColumn = 0;
                    if (this.CurrentColumn < this.leftColumn)
                    {
                        fullNeedsDisplay = true;
                        this.leftColumn = 0;
                    }

                    if (fullNeedsDisplay)
                        this.SetNeedsDisplay();
                    else
                        this.SetNeedsDisplay(new Rect(0, this.CurrentRow - this.topRow, 0, this.Frame.Height));
                    break;

                default:
                    // Ignore control characters and other special keys
                    if (kb.Key < Key.Space || kb.Key > Key.CharMask)
                        return false;
                    this.Insert((uint) kb.Key);
                    this.CurrentColumn++;
                    if (this.CurrentColumn >= this.leftColumn + this.Frame.Width)
                    {
                        this.leftColumn++;
                        this.SetNeedsDisplay();
                    }

                    this.PositionCursor();
                    return true;
            }

            return true;
        }

        private IEnumerable<(int col, int row, Rune rune)> ForwardIterator(int col, int row)
        {
            if (col < 0 || row < 0)
                yield break;
            if (row >= this.model.Count)
                yield break;
            List<Rune> line = this.GetCurrentLine();
            if (col >= line.Count)
                yield break;

            while (row < this.model.Count)
            {
                for (int c = col; c < line.Count; c++)
                    yield return (c, row, line[c]);
                col = 0;
                row++;
                line = this.GetCurrentLine();
            }
        }

        private Rune RuneAt(int col, int row)
        {
            return this.model.GetLine(row)[col];
        }

        private bool MoveNext(ref int col, ref int row, out Rune rune)
        {
            List<Rune> line = this.model.GetLine(row);
            if (col + 1 < line.Count)
            {
                col++;
                rune = line[col];
                return true;
            }

            while (row + 1 < this.model.Count)
            {
                col = 0;
                row++;
                line = this.model.GetLine(row);
                if (line.Count > 0)
                {
                    rune = line[0];
                    return true;
                }
            }

            rune = 0;
            return false;
        }

        private bool MovePrev(ref int col, ref int row, out Rune rune)
        {
            List<Rune> line = this.model.GetLine(row);

            if (col > 0)
            {
                col--;
                rune = line[col];
                return true;
            }

            if (row == 0)
            {
                rune = 0;
                return false;
            }

            while (row > 0)
            {
                row--;
                line = this.model.GetLine(row);
                col = line.Count - 1;
                if (col >= 0)
                {
                    rune = line[col];
                    return true;
                }
            }

            rune = 0;
            return false;
        }

        private (int col, int row)? WordForward(int fromCol, int fromRow)
        {
            int col = fromCol;
            int row = fromRow;
            List<Rune> line = this.GetCurrentLine();
            Rune rune = this.RuneAt(col, row);

            int srow = row;
            if (Rune.IsPunctuation(rune) || Rune.IsWhiteSpace(rune))
            {
                while (this.MoveNext(ref col, ref row, out rune))
                    if (Rune.IsLetterOrDigit(rune))
                        break;
                while (this.MoveNext(ref col, ref row, out rune))
                    if (!Rune.IsLetterOrDigit(rune))
                        break;
            }
            else
            {
                while (this.MoveNext(ref col, ref row, out rune))
                    if (!Rune.IsLetterOrDigit(rune))
                        break;
            }

            if (fromCol != col || fromRow != row)
                return (col, row);
            return null;
        }

        private (int col, int row)? WordBackward(int fromCol, int fromRow)
        {
            if (fromRow == 0 && fromCol == 0)
                return null;

            int col = fromCol;
            int row = fromRow;
            List<Rune> line = this.GetCurrentLine();
            Rune rune = this.RuneAt(col, row);

            if (Rune.IsPunctuation(rune) || Rune.IsSymbol(rune) || Rune.IsWhiteSpace(rune))
            {
                while (this.MovePrev(ref col, ref row, out rune))
                    if (Rune.IsLetterOrDigit(rune))
                        break;
                while (this.MovePrev(ref col, ref row, out rune))
                    if (!Rune.IsLetterOrDigit(rune))
                        break;
            }
            else
            {
                while (this.MovePrev(ref col, ref row, out rune))
                    if (!Rune.IsLetterOrDigit(rune))
                        break;
            }

            if (fromCol != col || fromRow != row)
                return (col, row);
            return null;
        }

        public override bool MouseEvent(MouseEvent ev)
        {
            if (!ev.Flags.HasFlag(MouseFlags.Button1Clicked))
                return false;

            if (!this.HasFocus)
                this.SuperView.SetFocus(this);

            if (ev.Y + this.topRow >= this.model.Count)
                this.CurrentRow = this.model.Count - this.topRow;
            else
                this.CurrentRow = ev.Y + this.topRow;
            List<Rune> r = this.GetCurrentLine();
            if (ev.X - this.leftColumn >= r.Count)
                this.CurrentColumn = r.Count - this.leftColumn;
            else
                this.CurrentColumn = ev.X - this.leftColumn;

            this.PositionCursor();
            return true;
        }
    }
}