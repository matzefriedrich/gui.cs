//
// handles.cs: OO wrappers for some curses objects
//
// Authors:
//   Miguel de Icaza (miguel.de.icaza@gmail.com)
//
// Copyright (C) 2007 Novell (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

namespace Terminal.Gui.MonoCurses
{
    using System;

    internal partial class Curses
    {
        internal class Window
        {
            public readonly IntPtr Handle;

            static Window()
            {
                initscr();
                Standard = new Window(console_sharp_get_stdscr());
                Current = new Window(console_sharp_get_curscr());
            }

            internal Window(IntPtr handle)
            {
                this.Handle = handle;
            }

            public static Window Standard { get; }

            public static Window Current { get; }


            public int wtimeout(int delay)
            {
                return Curses.wtimeout(this.Handle, delay);
            }

            public int notimeout(bool bf)
            {
                return Curses.notimeout(this.Handle, bf);
            }

            public int keypad(bool bf)
            {
                return Curses.keypad(this.Handle, bf);
            }

            public int meta(bool bf)
            {
                return Curses.meta(this.Handle, bf);
            }

            public int intrflush(bool bf)
            {
                return Curses.intrflush(this.Handle, bf);
            }

            public int clearok(bool bf)
            {
                return Curses.clearok(this.Handle, bf);
            }

            public int idlok(bool bf)
            {
                return Curses.idlok(this.Handle, bf);
            }

            public void idcok(bool bf)
            {
                Curses.idcok(this.Handle, bf);
            }

            public void immedok(bool bf)
            {
                Curses.immedok(this.Handle, bf);
            }

            public int leaveok(bool bf)
            {
                return Curses.leaveok(this.Handle, bf);
            }

            public int setscrreg(int top, int bot)
            {
                return wsetscrreg(this.Handle, top, bot);
            }

            public int scrollok(bool bf)
            {
                return Curses.scrollok(this.Handle, bf);
            }

            public int wrefresh()
            {
                return Curses.wrefresh(this.Handle);
            }

            public int redrawwin()
            {
                return Curses.redrawwin(this.Handle);
            }

            public int wredrawwin(int beg_line, int num_lines)
            {
                return Curses.wredrawwin(this.Handle, beg_line, num_lines);
            }

            public int wnoutrefresh()
            {
                return Curses.wnoutrefresh(this.Handle);
            }

            public int move(int line, int col)
            {
                return wmove(this.Handle, line, col);
            }

            public int addch(char ch)
            {
                return waddch(this.Handle, ch);
            }

            public int refresh()
            {
                return Curses.wrefresh(this.Handle);
            }
        }

        // Currently unused, to do later
        internal class Screen
        {
            public readonly IntPtr Handle;

            internal Screen(IntPtr handle)
            {
                this.Handle = handle;
            }
        }
    }
}