//
// binding.cs.in: Core binding for curses.
//
// This file attempts to call into ncurses without relying on Mono's
// dllmap, so it will work with .NET Core.  This means that it needs
// two sets of bindings, one for "ncurses" which works on OSX, and one
// that works against "libncursesw.so.5" which is what you find on
// assorted Linux systems.
//
// Additionally, I do not want to rely on an external native library
// which is why all this pain to bind two separate ncurses is here.
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
    using System.Runtime.InteropServices;

    //
    // P/Invoke definitions for looking up symbols in the "ncurses" library, as resolved
    // by the dynamic linker, different than CursesLinux that looksup by "libncursesw.so.5"
    //

    //
    // P/Invoke definitions for looking up symbols in the "libncursesw.so.5" library, as resolved
    // by the dynamic linker, different than RegularCurses that looksup by "ncurses"
    //
    internal class CursesLinux
    {
        [DllImport("libncursesw.so.5", EntryPoint = "mousemask")]
        public static extern IntPtr call_mousemask(IntPtr newmask, out IntPtr oldmask);

        [DllImport("libncursesw.so.5", EntryPoint = "initscr")]
        internal static extern IntPtr real_initscr();

        [DllImport("libncursesw.so.5")]
        public static extern int endwin();

        [DllImport("libncursesw.so.5")]
        public static extern bool isendwin();

        //
        // Screen operations are flagged as internal, as we need to
        // catch all changes so we can update newscr, curscr, stdscr
        //
        [DllImport("libncursesw.so.5")]
        public static extern IntPtr internal_newterm(string type, IntPtr file_outfd, IntPtr file_infd);

        [DllImport("libncursesw.so.5")]
        public static extern IntPtr internal_set_term(IntPtr newscreen);

        [DllImport("libncursesw.so.5")]
        internal static extern void internal_delscreen(IntPtr sp);

        [DllImport("libncursesw.so.5")]
        public static extern int cbreak();

        [DllImport("libncursesw.so.5")]
        public static extern int nocbreak();

        [DllImport("libncursesw.so.5")]
        public static extern int echo();

        [DllImport("libncursesw.so.5")]
        public static extern int noecho();

        [DllImport("libncursesw.so.5")]
        public static extern int halfdelay(int t);

        [DllImport("libncursesw.so.5")]
        public static extern int raw();

        [DllImport("libncursesw.so.5")]
        public static extern int noraw();

        [DllImport("libncursesw.so.5")]
        public static extern void noqiflush();

        [DllImport("libncursesw.so.5")]
        public static extern void qiflush();

        [DllImport("libncursesw.so.5")]
        public static extern int typeahead(IntPtr fd);

        [DllImport("libncursesw.so.5")]
        public static extern int timeout(int delay);

        //
        // Internal, as they are exposed in Window
        //
        [DllImport("libncursesw.so.5")]
        internal static extern int wtimeout(IntPtr win, int delay);

        [DllImport("libncursesw.so.5")]
        internal static extern int notimeout(IntPtr win, bool bf);

        [DllImport("libncursesw.so.5")]
        internal static extern int keypad(IntPtr win, bool bf);

        [DllImport("libncursesw.so.5")]
        internal static extern int meta(IntPtr win, bool bf);

        [DllImport("libncursesw.so.5")]
        internal static extern int intrflush(IntPtr win, bool bf);

        [DllImport("libncursesw.so.5")]
        internal static extern int clearok(IntPtr win, bool bf);

        [DllImport("libncursesw.so.5")]
        internal static extern int idlok(IntPtr win, bool bf);

        [DllImport("libncursesw.so.5")]
        internal static extern void idcok(IntPtr win, bool bf);

        [DllImport("libncursesw.so.5")]
        internal static extern void immedok(IntPtr win, bool bf);

        [DllImport("libncursesw.so.5")]
        internal static extern int leaveok(IntPtr win, bool bf);

        [DllImport("libncursesw.so.5")]
        internal static extern int wsetscrreg(IntPtr win, int top, int bot);

        [DllImport("libncursesw.so.5")]
        internal static extern int scrollok(IntPtr win, bool bf);

        [DllImport("libncursesw.so.5")]
        public static extern int nl();

        [DllImport("libncursesw.so.5")]
        public static extern int nonl();

        [DllImport("libncursesw.so.5")]
        public static extern int setscrreg(int top, int bot);


        [DllImport("libncursesw.so.5")]
        public static extern int refresh();

        [DllImport("libncursesw.so.5")]
        public static extern int doupdate();

        [DllImport("libncursesw.so.5")]
        internal static extern int wrefresh(IntPtr win);

        [DllImport("libncursesw.so.5")]
        internal static extern int redrawwin(IntPtr win);

        [DllImport("libncursesw.so.5")]
        internal static extern int wredrawwin(IntPtr win, int beg_line, int num_lines);

        [DllImport("libncursesw.so.5")]
        internal static extern int wnoutrefresh(IntPtr win);

        [DllImport("libncursesw.so.5")]
        public static extern int move(int line, int col);

        [DllImport("libncursesw.so.5", EntryPoint = "addch")]
        internal static extern int _addch(int ch);

        [DllImport("libncursesw.so.5")]
        public static extern int addstr(string s);

        [DllImport("libncursesw.so.5")]
        internal static extern int wmove(IntPtr win, int line, int col);

        [DllImport("libncursesw.so.5")]
        internal static extern int waddch(IntPtr win, int ch);

        [DllImport("libncursesw.so.5")]
        public static extern int attron(int attrs);

        [DllImport("libncursesw.so.5")]
        public static extern int attroff(int attrs);

        [DllImport("libncursesw.so.5")]
        public static extern int attrset(int attrs);

        [DllImport("libncursesw.so.5")]
        public static extern int getch();

        [DllImport("libncursesw.so.5")]
        public static extern int get_wch(out int sequence);

        [DllImport("libncursesw.so.5")]
        public static extern int ungetch(int ch);

        [DllImport("libncursesw.so.5")]
        public static extern int mvgetch(int y, int x);

        [DllImport("libncursesw.so.5")]
        internal static extern bool has_colors();

        [DllImport("libncursesw.so.5")]
        internal static extern int start_color();

        [DllImport("libncursesw.so.5")]
        internal static extern int init_pair(short pair, short f, short b);

        [DllImport("libncursesw.so.5")]
        internal static extern int use_default_colors();

        [DllImport("libncursesw.so.5")]
        internal static extern int COLOR_PAIRS();

        [DllImport("libncursesw.so.5")]
        public static extern uint getmouse(out Curses.MouseEvent ev);

        [DllImport("libncursesw.so.5")]
        public static extern uint ungetmouse(ref Curses.MouseEvent ev);

        [DllImport("libncursesw.so.5")]
        public static extern int mouseinterval(int interval);
    }
}