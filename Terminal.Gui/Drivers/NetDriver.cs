//
// NetDriver.cs: The System.Console-based .NET driver, works on Windows and Unix, but is not particularly efficient.
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//

namespace Terminal.Gui.Drivers
{
    using System;

    using NStack;

    using Terminal.Gui.MonoCurses;
    using Terminal.Gui.Types;

    internal class NetDriver : ConsoleDriver
    {
        private static bool sync;

        // Current row, and current col, tracked by Move/AddCh only
        private int ccol, crow;

        // The format is rows, columns and 3 values on the last column: Rune, Attribute and Dirty Flag
        private int[,,] contents;

        private int currentAttribute;

        private bool[] dirtyLine;

        private bool needMove;

        private int redrawColor = -1;

        public NetDriver()
        {
            this.Cols = Console.WindowWidth;
            this.Rows = Console.WindowHeight - 1;
            this.UpdateOffscreen();
        }

        public override int Cols { get; }

        public override int Rows { get; }

        private void UpdateOffscreen()
        {
            int cols = this.Cols;
            int rows = this.Rows;

            this.contents = new int [rows, cols, 3];
            for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                this.contents[r, c, 0] = ' ';
                this.contents[r, c, 1] = MakeColor(ConsoleColor.Gray, ConsoleColor.Black);
                this.contents[r, c, 2] = 0;
            }

            this.dirtyLine = new bool [rows];
            for (var row = 0; row < rows; row++)
                this.dirtyLine[row] = true;
        }

        public override void Move(int col, int row)
        {
            this.ccol = col;
            this.crow = row;

            if (this.Clip.Contains(col, row))
            {
                Console.CursorTop = row;
                Console.CursorLeft = col;
                this.needMove = false;
            }
            else
            {
                Console.CursorTop = this.Clip.Y;
                Console.CursorLeft = this.Clip.X;
                this.needMove = true;
            }
        }

        public override void AddRune(Rune rune)
        {
            if (this.Clip.Contains(this.ccol, this.crow))
            {
                if (this.needMove)
                    this.needMove = false;
                this.contents[this.crow, this.ccol, 0] = (int) (uint) rune;
                this.contents[this.crow, this.ccol, 1] = this.currentAttribute;
                this.contents[this.crow, this.ccol, 2] = 1;
                this.dirtyLine[this.crow] = true;
            }
            else
            {
                this.needMove = true;
            }

            this.ccol++;
            if (this.ccol == this.Cols)
            {
                this.ccol = 0;
                if (this.crow + 1 < this.Rows)
                    this.crow++;
            }

            if (sync)
                this.UpdateScreen();
        }

        public override void AddStr(ustring str)
        {
            foreach (uint rune in str)
                this.AddRune(rune);
        }

        public override void End()
        {
            Console.ResetColor();
            Console.Clear();
        }

        private static Attribute MakeColor(ConsoleColor f, ConsoleColor b)
        {
            // Encode the colors into the int value.
            return new Attribute {value = (((int) f & 0xffff) << 16) | ((int) b & 0xffff)};
        }


        public override void Init(Action terminalResized)
        {
            Colors.Base = new ColorScheme();
            Colors.Dialog = new ColorScheme();
            Colors.Menu = new ColorScheme();
            Colors.Error = new ColorScheme();
            this.Clip = new Rect(0, 0, this.Cols, this.Rows);

            this.HLine = '\u2500';
            this.VLine = '\u2502';
            this.Stipple = '\u2592';
            this.Diamond = '\u25c6';
            this.ULCorner = '\u250C';
            this.LLCorner = '\u2514';
            this.URCorner = '\u2510';
            this.LRCorner = '\u2518';
            this.LeftTee = '\u251c';
            this.RightTee = '\u2524';
            this.TopTee = '\u22a4';
            this.BottomTee = '\u22a5';

            Colors.Base.Normal = MakeColor(ConsoleColor.White, ConsoleColor.Blue);
            Colors.Base.Focus = MakeColor(ConsoleColor.Black, ConsoleColor.Cyan);
            Colors.Base.HotNormal = MakeColor(ConsoleColor.Yellow, ConsoleColor.Blue);
            Colors.Base.HotFocus = MakeColor(ConsoleColor.Yellow, ConsoleColor.Cyan);

            // Focused, 
            //    Selected, Hot: Yellow on Black
            //    Selected, text: white on black
            //    Unselected, hot: yellow on cyan
            //    unselected, text: same as unfocused
            Colors.Menu.HotFocus = MakeColor(ConsoleColor.Yellow, ConsoleColor.Black);
            Colors.Menu.Focus = MakeColor(ConsoleColor.White, ConsoleColor.Black);
            Colors.Menu.HotNormal = MakeColor(ConsoleColor.Yellow, ConsoleColor.Cyan);
            Colors.Menu.Normal = MakeColor(ConsoleColor.White, ConsoleColor.Cyan);

            Colors.Dialog.Normal = MakeColor(ConsoleColor.Black, ConsoleColor.Gray);
            Colors.Dialog.Focus = MakeColor(ConsoleColor.Black, ConsoleColor.Cyan);
            Colors.Dialog.HotNormal = MakeColor(ConsoleColor.Blue, ConsoleColor.Gray);
            Colors.Dialog.HotFocus = MakeColor(ConsoleColor.Blue, ConsoleColor.Cyan);

            Colors.Error.Normal = MakeColor(ConsoleColor.White, ConsoleColor.Red);
            Colors.Error.Focus = MakeColor(ConsoleColor.Black, ConsoleColor.Gray);
            Colors.Error.HotNormal = MakeColor(ConsoleColor.Yellow, ConsoleColor.Red);
            Colors.Error.HotFocus = Colors.Error.HotNormal;
            Console.Clear();
        }

        private void SetColor(int color)
        {
            this.redrawColor = color;
            Console.BackgroundColor = (ConsoleColor) (color & 0xffff);
            Console.ForegroundColor = (ConsoleColor) ((color >> 16) & 0xffff);
        }

        public override void UpdateScreen()
        {
            int rows = this.Rows;
            int cols = this.Cols;

            Console.CursorTop = 0;
            Console.CursorLeft = 0;
            for (var row = 0; row < rows; row++)
            {
                this.dirtyLine[row] = false;
                for (var col = 0; col < cols; col++)
                {
                    this.contents[row, col, 2] = 0;
                    int color = this.contents[row, col, 1];
                    if (color != this.redrawColor)
                        this.SetColor(color);
                    Console.Write((char) this.contents[row, col, 0]);
                }
            }
        }

        public override void Refresh()
        {
            int rows = this.Rows;
            int cols = this.Cols;

            int savedRow = Console.CursorTop;
            int savedCol = Console.CursorLeft;
            for (var row = 0; row < rows; row++)
            {
                if (!this.dirtyLine[row])
                    continue;
                this.dirtyLine[row] = false;
                for (var col = 0; col < cols; col++)
                {
                    if (this.contents[row, col, 2] != 1)
                        continue;

                    Console.CursorTop = row;
                    Console.CursorLeft = col;
                    for (; col < cols && this.contents[row, col, 2] == 1; col++)
                    {
                        int color = this.contents[row, col, 1];
                        if (color != this.redrawColor)
                            this.SetColor(color);

                        Console.Write((char) this.contents[row, col, 0]);
                        this.contents[row, col, 2] = 0;
                    }
                }
            }

            Console.CursorTop = savedRow;
            Console.CursorLeft = savedCol;
        }

        public override void UpdateCursor()
        {
            //
        }

        public override void StartReportingMouseMoves()
        {
        }

        public override void StopReportingMouseMoves()
        {
        }

        public override void Suspend()
        {
        }

        public override void SetAttribute(Attribute c)
        {
            this.currentAttribute = c.value;
        }

        private Key MapKey(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.Escape:
                    return Key.Esc;
                case ConsoleKey.Tab:
                    return keyInfo.Modifiers == ConsoleModifiers.Shift ? Key.BackTab : Key.Tab;
                case ConsoleKey.Home:
                    return Key.Home;
                case ConsoleKey.End:
                    return Key.End;
                case ConsoleKey.LeftArrow:
                    return Key.CursorLeft;
                case ConsoleKey.RightArrow:
                    return Key.CursorRight;
                case ConsoleKey.UpArrow:
                    return Key.CursorUp;
                case ConsoleKey.DownArrow:
                    return Key.CursorDown;
                case ConsoleKey.PageUp:
                    return Key.PageUp;
                case ConsoleKey.PageDown:
                    return Key.PageDown;
                case ConsoleKey.Enter:
                    return Key.Enter;
                case ConsoleKey.Spacebar:
                    return Key.Space;
                case ConsoleKey.Backspace:
                    return Key.Backspace;
                case ConsoleKey.Delete:
                    return Key.Delete;

                case ConsoleKey.Oem1:
                case ConsoleKey.Oem2:
                case ConsoleKey.Oem3:
                case ConsoleKey.Oem4:
                case ConsoleKey.Oem5:
                case ConsoleKey.Oem6:
                case ConsoleKey.Oem7:
                case ConsoleKey.Oem8:
                case ConsoleKey.Oem102:
                case ConsoleKey.OemPeriod:
                case ConsoleKey.OemComma:
                case ConsoleKey.OemPlus:
                case ConsoleKey.OemMinus:
                    return (Key) keyInfo.KeyChar;
            }

            ConsoleKey key = keyInfo.Key;
            if (key >= ConsoleKey.A && key <= ConsoleKey.Z)
            {
                int delta = key - ConsoleKey.A;
                if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    return (Key) ((uint) Key.ControlA + delta);
                if (keyInfo.Modifiers == ConsoleModifiers.Alt)
                    return (Key) ((uint) Key.AltMask | ((uint) 'A' + delta));
                if (keyInfo.Modifiers == ConsoleModifiers.Shift)
                    return (Key) ((uint) 'A' + delta);
                return (Key) ((uint) 'a' + delta);
            }

            if (key >= ConsoleKey.D0 && key <= ConsoleKey.D9)
            {
                int delta = key - ConsoleKey.D0;
                if (keyInfo.Modifiers == ConsoleModifiers.Alt)
                    return (Key) ((uint) Key.AltMask | ((uint) '0' + delta));
                if (keyInfo.Modifiers == ConsoleModifiers.Shift)
                    return (Key) keyInfo.KeyChar;
                return (Key) ((uint) '0' + delta);
            }

            if (key >= ConsoleKey.F1 && key <= ConsoleKey.F10)
            {
                int delta = key - ConsoleKey.F1;

                return (Key) ((int) Key.F1 + delta);
            }

            return (Key) 0xffffffff;
        }

        public override void PrepareToRun(MainLoop mainLoop, Action<KeyEvent> keyHandler, Action<MouseEvent> mouseHandler)
        {
            (mainLoop.Driver as NetMainLoop).WindowsKeyPressed = delegate(ConsoleKeyInfo consoleKey)
            {
                Key map = this.MapKey(consoleKey);
                if (map == (Key) 0xffffffff)
                    return;
                keyHandler(new KeyEvent(map));
            };
        }

        public override void SetColors(ConsoleColor foreground, ConsoleColor background)
        {
            throw new NotImplementedException();
        }

        public override void SetColors(short foregroundColorId, short backgroundColorId)
        {
            throw new NotImplementedException();
        }

        public override void CookMouse()
        {
        }

        public override void UncookMouse()
        {
        }

        //
        // These are for the .NET driver, but running natively on Windows, wont run 
        // on the Mono emulation
        //
    }
}