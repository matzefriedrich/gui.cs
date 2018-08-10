﻿//
// Driver.cs: Curses-based Driver
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//

namespace Terminal.Gui
{
    using System;
    using System.Collections.Generic;

    using Mono.Terminal;

    using NStack;

    using Unix.Terminal;

    /// <summary>
    ///     This is the Curses driver for the gui.cs/Terminal framework.
    /// </summary>
    internal class CursesDriver : ConsoleDriver
    {
        private static bool sync;

        private static short last_color_pair = 16;

        private readonly int[,] colorPairs = new int [16, 16];

        private readonly Dictionary<int, int> rawPairs = new Dictionary<int, int>();

        // Current row, and current col, tracked by Move/AddRune only
        private int ccol, crow;

        private int lastMouseInterval;

        private bool mouseGrabbed;

        private bool needMove;

        private Curses.Event oldMouseEvents, reportableMouseEvents;

        private Action terminalResized;

        public Curses.Window window;

        public override int Cols => Curses.Cols;

        public override int Rows => Curses.Lines;

        public override void Move(int col, int row)
        {
            this.ccol = col;
            this.crow = row;

            if (this.Clip.Contains(col, row))
            {
                Curses.move(row, col);
                this.needMove = false;
            }
            else
            {
                Curses.move(this.Clip.Y, this.Clip.X);
                this.needMove = true;
            }
        }

        public override void AddRune(Rune rune)
        {
            if (this.Clip.Contains(this.ccol, this.crow))
            {
                if (this.needMove)
                {
                    Curses.move(this.crow, this.ccol);
                    this.needMove = false;
                }

                Curses.addch((int) (uint) rune);
            }
            else
            {
                this.needMove = true;
            }

            if (sync)
                Application.Driver.Refresh();
            this.ccol++;
        }

        public override void AddStr(ustring str)
        {
            // TODO; optimize this to determine if the str fits in the clip region, and if so, use Curses.addstr directly
            foreach (uint rune in str)
                this.AddRune(rune);
        }

        public override void Refresh()
        {
            Curses.refresh();
        }

        public override void UpdateCursor()
        {
            Curses.refresh();
        }

        public override void End()
        {
            Curses.endwin();
        }

        public override void UpdateScreen()
        {
            this.window.redrawwin();
        }

        public override void SetAttribute(Attribute c)
        {
            Curses.attrset(c.value);
        }

        private static Attribute MakeColor(short f, short b)
        {
            Curses.InitColorPair(++last_color_pair, f, b);
            return new Attribute {value = Curses.ColorPair(last_color_pair)};
        }

        public override void SetColors(ConsoleColor foreground, ConsoleColor background)
        {
            int f = (short) foreground;
            int b = (short) background;
            int v = this.colorPairs[f, b];
            if ((v & 0x10000) == 0)
            {
                b = b & 0x7;
                bool bold = (f & 0x8) != 0;
                f = f & 0x7;

                v = MakeColor((short) f, (short) b) | (bold ? Curses.A_BOLD : 0);
                this.colorPairs[(int) foreground, (int) background] = v | 0x1000;
            }

            this.SetAttribute(v & 0xffff);
        }

        public override void SetColors(short foreColorId, short backgroundColorId)
        {
            int key = ((ushort) foreColorId << 16) | (ushort) backgroundColorId;
            if (!this.rawPairs.TryGetValue(key, out int v))
            {
                v = MakeColor(foreColorId, backgroundColorId);
                this.rawPairs[key] = v;
            }

            this.SetAttribute(v);
        }

        private static Key MapCursesKey(int cursesKey)
        {
            switch (cursesKey)
            {
                case Curses.KeyF1: return Key.F1;
                case Curses.KeyF2: return Key.F2;
                case Curses.KeyF3: return Key.F3;
                case Curses.KeyF4: return Key.F4;
                case Curses.KeyF5: return Key.F5;
                case Curses.KeyF6: return Key.F6;
                case Curses.KeyF7: return Key.F7;
                case Curses.KeyF8: return Key.F8;
                case Curses.KeyF9: return Key.F9;
                case Curses.KeyF10: return Key.F10;
                case Curses.KeyUp: return Key.CursorUp;
                case Curses.KeyDown: return Key.CursorDown;
                case Curses.KeyLeft: return Key.CursorLeft;
                case Curses.KeyRight: return Key.CursorRight;
                case Curses.KeyHome: return Key.Home;
                case Curses.KeyEnd: return Key.End;
                case Curses.KeyNPage: return Key.PageDown;
                case Curses.KeyPPage: return Key.PageUp;
                case Curses.KeyDeleteChar: return Key.DeleteChar;
                case Curses.KeyInsertChar: return Key.InsertChar;
                case Curses.KeyBackTab: return Key.BackTab;
                case Curses.KeyBackspace: return Key.Backspace;
                default: return Key.Unknown;
            }
        }

        private static MouseEvent ToDriverMouse(Curses.MouseEvent cev)
        {
            return new MouseEvent
            {
                X = cev.X,
                Y = cev.Y,
                Flags = (MouseFlags) cev.ButtonState
            };
        }

        private void ProcessInput(Action<KeyEvent> keyHandler, Action<MouseEvent> mouseHandler)
        {
            int wch;
            int code = Curses.get_wch(out wch);
            if (code == Curses.ERR)
                return;
            if (code == Curses.KEY_CODE_YES)
            {
                if (wch == Curses.KeyResize)
                    if (Curses.CheckWinChange())
                    {
                        this.terminalResized();
                        return;
                    }

                if (wch == Curses.KeyMouse)
                {
                    Curses.MouseEvent ev;
                    Curses.getmouse(out ev);
                    mouseHandler(ToDriverMouse(ev));
                    return;
                }

                keyHandler(new KeyEvent(MapCursesKey(wch)));
                return;
            }

            // Special handling for ESC, we want to try to catch ESC+letter to simulate alt-letter as well as Alt-Fkey
            if (wch == 27)
            {
                Curses.timeout(200);

                code = Curses.get_wch(out wch);
                if (code == Curses.KEY_CODE_YES)
                    keyHandler(new KeyEvent(Key.AltMask | MapCursesKey(wch)));
                if (code == 0)
                {
                    KeyEvent key;

                    // The ESC-number handling, debatable.
                    if (wch >= '1' && wch <= '9')
                        key = new KeyEvent((Key) ((int) Key.F1 + (wch - '0' - 1)));
                    else if (wch == '0')
                        key = new KeyEvent(Key.F10);
                    else if (wch == 27)
                        key = new KeyEvent((Key) wch);
                    else
                        key = new KeyEvent(Key.AltMask | (Key) wch);
                    keyHandler(key);
                }
                else
                {
                    keyHandler(new KeyEvent(Key.Esc));
                }
            }
            else
            {
                keyHandler(new KeyEvent((Key) wch));
            }
        }

        public override void PrepareToRun(MainLoop mainLoop, Action<KeyEvent> keyHandler, Action<MouseEvent> mouseHandler)
        {
            Curses.timeout(-1);

            (mainLoop.Driver as UnixMainLoop).AddWatch(0, UnixMainLoop.Condition.PollIn, x =>
            {
                this.ProcessInput(keyHandler, mouseHandler);
                return true;
            });
        }

        public override void Init(Action terminalResized)
        {
            if (this.window != null)
                return;

            try
            {
                this.window = Curses.initscr();
            }
            catch (Exception e)
            {
                Console.WriteLine("Curses failed to initialize, the exception is: " + e);
            }

            Curses.raw();
            Curses.noecho();

            Curses.Window.Standard.keypad(true);
            this.reportableMouseEvents = Curses.mousemask(Curses.Event.AllEvents | Curses.Event.ReportMousePosition, out this.oldMouseEvents);
            this.terminalResized = terminalResized;
            this.StartReportingMouseMoves();

            this.HLine = Curses.ACS_HLINE;
            this.VLine = Curses.ACS_VLINE;
            this.Stipple = Curses.ACS_CKBOARD;
            this.Diamond = Curses.ACS_DIAMOND;
            this.ULCorner = Curses.ACS_ULCORNER;
            this.LLCorner = Curses.ACS_LLCORNER;
            this.URCorner = Curses.ACS_URCORNER;
            this.LRCorner = Curses.ACS_LRCORNER;
            this.LeftTee = Curses.ACS_LTEE;
            this.RightTee = Curses.ACS_RTEE;
            this.TopTee = Curses.ACS_TTEE;
            this.BottomTee = Curses.ACS_BTEE;

            Colors.Base = new ColorScheme();
            Colors.Dialog = new ColorScheme();
            Colors.Menu = new ColorScheme();
            Colors.Error = new ColorScheme();
            this.Clip = new Rect(0, 0, this.Cols, this.Rows);
            if (Curses.HasColors)
            {
                Curses.StartColor();
                Curses.UseDefaultColors();

                Colors.Base.Normal = MakeColor(Curses.COLOR_WHITE, Curses.COLOR_BLUE);
                Colors.Base.Focus = MakeColor(Curses.COLOR_BLACK, Curses.COLOR_CYAN);
                Colors.Base.HotNormal = Curses.A_BOLD | MakeColor(Curses.COLOR_YELLOW, Curses.COLOR_BLUE);
                Colors.Base.HotFocus = Curses.A_BOLD | MakeColor(Curses.COLOR_YELLOW, Curses.COLOR_CYAN);

                // Focused, 
                //    Selected, Hot: Yellow on Black
                //    Selected, text: white on black
                //    Unselected, hot: yellow on cyan
                //    unselected, text: same as unfocused
                Colors.Menu.HotFocus = Curses.A_BOLD | MakeColor(Curses.COLOR_YELLOW, Curses.COLOR_BLACK);
                Colors.Menu.Focus = Curses.A_BOLD | MakeColor(Curses.COLOR_WHITE, Curses.COLOR_BLACK);
                Colors.Menu.HotNormal = Curses.A_BOLD | MakeColor(Curses.COLOR_YELLOW, Curses.COLOR_CYAN);
                Colors.Menu.Normal = Curses.A_BOLD | MakeColor(Curses.COLOR_WHITE, Curses.COLOR_CYAN);

                Colors.Dialog.Normal = MakeColor(Curses.COLOR_BLACK, Curses.COLOR_WHITE);
                Colors.Dialog.Focus = MakeColor(Curses.COLOR_BLACK, Curses.COLOR_CYAN);
                Colors.Dialog.HotNormal = MakeColor(Curses.COLOR_BLUE, Curses.COLOR_WHITE);
                Colors.Dialog.HotFocus = MakeColor(Curses.COLOR_BLUE, Curses.COLOR_CYAN);

                Colors.Error.Normal = Curses.A_BOLD | MakeColor(Curses.COLOR_WHITE, Curses.COLOR_RED);
                Colors.Error.Focus = MakeColor(Curses.COLOR_BLACK, Curses.COLOR_WHITE);
                Colors.Error.HotNormal = Curses.A_BOLD | MakeColor(Curses.COLOR_YELLOW, Curses.COLOR_RED);
                Colors.Error.HotFocus = Colors.Error.HotNormal;
            }
            else
            {
                Colors.Base.Normal = Curses.A_NORMAL;
                Colors.Base.Focus = Curses.A_REVERSE;
                Colors.Base.HotNormal = Curses.A_BOLD;
                Colors.Base.HotFocus = Curses.A_BOLD | Curses.A_REVERSE;
                Colors.Menu.Normal = Curses.A_REVERSE;
                Colors.Menu.Focus = Curses.A_NORMAL;
                Colors.Menu.HotNormal = Curses.A_BOLD;
                Colors.Menu.HotFocus = Curses.A_NORMAL;
                Colors.Dialog.Normal = Curses.A_REVERSE;
                Colors.Dialog.Focus = Curses.A_NORMAL;
                Colors.Dialog.HotNormal = Curses.A_BOLD;
                Colors.Dialog.HotFocus = Curses.A_NORMAL;
                Colors.Error.Normal = Curses.A_BOLD;
                Colors.Error.Focus = Curses.A_BOLD | Curses.A_REVERSE;
                Colors.Error.HotNormal = Curses.A_BOLD | Curses.A_REVERSE;
                Colors.Error.HotFocus = Curses.A_REVERSE;
            }
        }

        public override void Suspend()
        {
            this.StopReportingMouseMoves();
            Platform.Suspend();
            Curses.Window.Standard.redrawwin();
            Curses.refresh();
            this.StartReportingMouseMoves();
        }

        public override void StartReportingMouseMoves()
        {
            Console.Out.Write("\x1b[?1003h");
            Console.Out.Flush();
        }

        public override void StopReportingMouseMoves()
        {
            Console.Out.Write("\x1b[?1003l");
            Console.Out.Flush();
        }

        public override void UncookMouse()
        {
            if (this.mouseGrabbed)
                return;
            this.lastMouseInterval = Curses.mouseinterval(0);
            this.mouseGrabbed = true;
        }

        public override void CookMouse()
        {
            this.mouseGrabbed = false;
            Curses.mouseinterval(this.lastMouseInterval);
        }
    }
}