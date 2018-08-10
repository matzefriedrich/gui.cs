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

namespace Unix.Terminal {
	using System;
	using System.IO;
	using System.Runtime.InteropServices;

	partial class Curses {
		// We encode ESC + char (what Alt-char generates) as 0x2000 + char
		public const int KeyAlt = 0x2000;

		static int lines, cols;

		static Window main_window;

		static IntPtr curses_handle, curscr_ptr, lines_ptr, cols_ptr;

		// If true, uses the DllImport into "ncurses", otherwise "libncursesw.so.5"
		static bool use_naked_driver;

		static char[] r = new char [1];

		static bool uselibc;

		static IntPtr stdscr;

		public static int Lines => lines;

		public static int Cols => cols;

		public static bool HasColors => has_colors();

		public static int ColorPairs => COLOR_PAIRS();

		//
		// Ugly hack to P/Invoke into either libc, or libdl, again, because
		// we can not have nice things - .NET Core in this day and age still
		// does not have <dllmap>
		//
		static IntPtr DlOpen(string path)
		{
			if (!uselibc)
				try {
					var handle = dlopen(path, 1);
					return handle;
				} catch (DllNotFoundException) {
					uselibc = true;
					return DlOpen(path);
				}

			return libc_dlopen(path, 1);
		}

		static void FindNCurses()
		{
			if (File.Exists("/usr/lib/libncurses.dylib")) {
				curses_handle = DlOpen("libncurses.dylib");
				use_naked_driver = true;
			} else
				curses_handle = DlOpen("libncursesw.so.5");

			if (curses_handle == IntPtr.Zero) {
				Console.WriteLine("It is not possible to open the dynamic library ncurses, tried looking for libncurses.dylib on Mac, and libncursesw.so.5 on Linux");
				Environment.Exit(1);
			}

			stdscr = read_static_ptr("stdscr");
			curscr_ptr = get_ptr("curscr");
			lines_ptr = get_ptr("LINES");
			cols_ptr = get_ptr("COLS");
		}

		public static Window initscr()
		{
			FindNCurses();

			main_window = new Window(real_initscr());
			try {
				console_sharp_get_dims(out lines, out cols);
			} catch (DllNotFoundException) {
				endwin();
				Console.Error.WriteLine("Unable to find the @MONO_CURSES@ native library\n" +
				                        "this is different than the managed mono-curses.dll\n\n" +
				                        "Typically you need to install to a LD_LIBRARY_PATH directory\n" +
				                        "or DYLD_LIBRARY_PATH directory or run /sbin/ldconfig");
				Environment.Exit(1);
			}

			return main_window;
		}

		//
		// Returns true if the window changed since the last invocation, as a
		// side effect, the Lines and Cols properties are updated
		//
		public static bool CheckWinChange()
		{
			int l, c;

			console_sharp_get_dims(out l, out c);
			if (l != lines || c != cols) {
				lines = l;
				cols = c;
				return true;
			}

			return false;
		}

		public static int addstr(string format, params object[] args)
		{
			string s = string.Format(format, args);
			return addstr(s);
		}

		//
		// Have to wrap the native addch, as it can not
		// display unicode characters, we have to use addstr
		// for that.   but we need addch to render special ACS
		// characters
		//
		public static int addch(int ch)
		{
			if (ch < 127 || ch > 0xffff)
				return _addch(ch);
			var c = (char) ch;
			return addstr(new string(c, 1));
		}

		[DllImport("dl")]
		static extern IntPtr dlopen(string file, int mode);

		[DllImport("dl")]
		static extern IntPtr dlsym(IntPtr handle, string symbol);

		[DllImport("libc", EntryPoint = "dlopen")]
		static extern IntPtr libc_dlopen(string file, int mode);

		[DllImport("libc", EntryPoint = "dlsym")]
		static extern IntPtr libc_dlsym(IntPtr handle, string symbol);

		static IntPtr get_ptr(string key)
		{
			var ptr = uselibc ? libc_dlsym(curses_handle, key) : dlsym(curses_handle, key);

			if (ptr == IntPtr.Zero)
				throw new Exception("Could not load the key " + key);
			return ptr;
		}

		internal static IntPtr read_static_ptr(string key)
		{
			var ptr = get_ptr(key);
			return Marshal.ReadIntPtr(ptr);
		}

		internal static IntPtr console_sharp_get_stdscr()
		{
			return stdscr;
		}


		internal static IntPtr console_sharp_get_curscr()
		{
			return Marshal.ReadIntPtr(curscr_ptr);
		}

		internal static void console_sharp_get_dims(out int lines, out int cols)
		{
			lines = Marshal.ReadInt32(lines_ptr);
			cols = Marshal.ReadInt32(cols_ptr);
		}

		public static Event mousemask(Event newmask, out Event oldmask)
		{
			IntPtr e;
			var ret = (Event) (use_naked_driver ? RegularCurses.call_mousemask((IntPtr) newmask, out e) : CursesLinux.call_mousemask((IntPtr) newmask, out e));
			oldmask = (Event) e;
			return ret;
		}

		public static int IsAlt(int key)
		{
			if ((key & KeyAlt) != 0)
				return key & ~KeyAlt;
			return 0;
		}

		public static int StartColor()
		{
			return start_color();
		}

		public static int InitColorPair(short pair, short foreground, short background)
		{
			return init_pair(pair, foreground, background);
		}

		public static int UseDefaultColors()
		{
			return use_default_colors();
		}


		//
		// The proxy methods to call into each version
		//
		public static IntPtr real_initscr()
		{
			return use_naked_driver ? RegularCurses.real_initscr() : CursesLinux.real_initscr();
		}

		public static int endwin()
		{
			return use_naked_driver ? RegularCurses.endwin() : CursesLinux.endwin();
		}

		public static bool isendwin()
		{
			return use_naked_driver ? RegularCurses.isendwin() : CursesLinux.isendwin();
		}

		public static IntPtr internal_newterm(string type, IntPtr file_outfd, IntPtr file_infd)
		{
			return use_naked_driver ? RegularCurses.internal_newterm(type, file_outfd, file_infd) : CursesLinux.internal_newterm(type, file_outfd, file_infd);
		}

		public static IntPtr internal_set_term(IntPtr newscreen)
		{
			return use_naked_driver ? RegularCurses.internal_set_term(newscreen) : CursesLinux.internal_set_term(newscreen);
		}

		public static void internal_delscreen(IntPtr sp)
		{
			if (use_naked_driver) RegularCurses.internal_delscreen(sp);
			else CursesLinux.internal_delscreen(sp);
		}

		public static int cbreak()
		{
			return use_naked_driver ? RegularCurses.cbreak() : CursesLinux.cbreak();
		}

		public static int nocbreak()
		{
			return use_naked_driver ? RegularCurses.nocbreak() : CursesLinux.nocbreak();
		}

		public static int echo()
		{
			return use_naked_driver ? RegularCurses.echo() : CursesLinux.echo();
		}

		public static int noecho()
		{
			return use_naked_driver ? RegularCurses.noecho() : CursesLinux.noecho();
		}

		public static int halfdelay(int t)
		{
			return use_naked_driver ? RegularCurses.halfdelay(t) : CursesLinux.halfdelay(t);
		}

		public static int raw()
		{
			return use_naked_driver ? RegularCurses.raw() : CursesLinux.raw();
		}

		public static int noraw()
		{
			return use_naked_driver ? RegularCurses.noraw() : CursesLinux.noraw();
		}

		public static void noqiflush()
		{
			if (use_naked_driver) RegularCurses.noqiflush();
			else CursesLinux.noqiflush();
		}

		public static void qiflush()
		{
			if (use_naked_driver) RegularCurses.qiflush();
			else CursesLinux.qiflush();
		}

		public static int typeahead(IntPtr fd)
		{
			return use_naked_driver ? RegularCurses.typeahead(fd) : CursesLinux.typeahead(fd);
		}

		public static int timeout(int delay)
		{
			return use_naked_driver ? RegularCurses.timeout(delay) : CursesLinux.timeout(delay);
		}

		public static int wtimeout(IntPtr win, int delay)
		{
			return use_naked_driver ? RegularCurses.wtimeout(win, delay) : CursesLinux.wtimeout(win, delay);
		}

		public static int notimeout(IntPtr win, bool bf)
		{
			return use_naked_driver ? RegularCurses.notimeout(win, bf) : CursesLinux.notimeout(win, bf);
		}

		public static int keypad(IntPtr win, bool bf)
		{
			return use_naked_driver ? RegularCurses.keypad(win, bf) : CursesLinux.keypad(win, bf);
		}

		public static int meta(IntPtr win, bool bf)
		{
			return use_naked_driver ? RegularCurses.meta(win, bf) : CursesLinux.meta(win, bf);
		}

		public static int intrflush(IntPtr win, bool bf)
		{
			return use_naked_driver ? RegularCurses.intrflush(win, bf) : CursesLinux.intrflush(win, bf);
		}

		public static int clearok(IntPtr win, bool bf)
		{
			return use_naked_driver ? RegularCurses.clearok(win, bf) : CursesLinux.clearok(win, bf);
		}

		public static int idlok(IntPtr win, bool bf)
		{
			return use_naked_driver ? RegularCurses.idlok(win, bf) : CursesLinux.idlok(win, bf);
		}

		public static void idcok(IntPtr win, bool bf)
		{
			if (use_naked_driver) RegularCurses.idcok(win, bf);
			else CursesLinux.idcok(win, bf);
		}

		public static void immedok(IntPtr win, bool bf)
		{
			if (use_naked_driver) RegularCurses.immedok(win, bf);
			else CursesLinux.immedok(win, bf);
		}

		public static int leaveok(IntPtr win, bool bf)
		{
			return use_naked_driver ? RegularCurses.leaveok(win, bf) : CursesLinux.leaveok(win, bf);
		}

		public static int wsetscrreg(IntPtr win, int top, int bot)
		{
			return use_naked_driver ? RegularCurses.wsetscrreg(win, top, bot) : CursesLinux.wsetscrreg(win, top, bot);
		}

		public static int scrollok(IntPtr win, bool bf)
		{
			return use_naked_driver ? RegularCurses.scrollok(win, bf) : CursesLinux.scrollok(win, bf);
		}

		public static int nl()
		{
			return use_naked_driver ? RegularCurses.nl() : CursesLinux.nl();
		}

		public static int nonl()
		{
			return use_naked_driver ? RegularCurses.nonl() : CursesLinux.nonl();
		}

		public static int setscrreg(int top, int bot)
		{
			return use_naked_driver ? RegularCurses.setscrreg(top, bot) : CursesLinux.setscrreg(top, bot);
		}

		public static int refresh()
		{
			return use_naked_driver ? RegularCurses.refresh() : CursesLinux.refresh();
		}

		public static int doupdate()
		{
			return use_naked_driver ? RegularCurses.doupdate() : CursesLinux.doupdate();
		}

		public static int wrefresh(IntPtr win)
		{
			return use_naked_driver ? RegularCurses.wrefresh(win) : CursesLinux.wrefresh(win);
		}

		public static int redrawwin(IntPtr win)
		{
			return use_naked_driver ? RegularCurses.redrawwin(win) : CursesLinux.redrawwin(win);
		}

		public static int wredrawwin(IntPtr win, int beg_line, int num_lines)
		{
			return use_naked_driver ? RegularCurses.wredrawwin(win, beg_line, num_lines) : CursesLinux.wredrawwin(win, beg_line, lines);
		}

		public static int wnoutrefresh(IntPtr win)
		{
			return use_naked_driver ? RegularCurses.wnoutrefresh(win) : CursesLinux.wnoutrefresh(win);
		}

		public static int move(int line, int col)
		{
			return use_naked_driver ? RegularCurses.move(line, col) : CursesLinux.move(line, col);
		}

		public static int _addch(int ch)
		{
			return use_naked_driver ? RegularCurses._addch(ch) : CursesLinux._addch(ch);
		}

		public static int addstr(string s)
		{
			return use_naked_driver ? RegularCurses.addstr(s) : CursesLinux.addstr(s);
		}

		public static int wmove(IntPtr win, int line, int col)
		{
			return use_naked_driver ? RegularCurses.wmove(win, line, col) : CursesLinux.wmove(win, line, col);
		}

		public static int waddch(IntPtr win, int ch)
		{
			return use_naked_driver ? RegularCurses.waddch(win, ch) : CursesLinux.waddch(win, ch);
		}

		public static int attron(int attrs)
		{
			return use_naked_driver ? RegularCurses.attron(attrs) : CursesLinux.attron(attrs);
		}

		public static int attroff(int attrs)
		{
			return use_naked_driver ? RegularCurses.attroff(attrs) : CursesLinux.attroff(attrs);
		}

		public static int attrset(int attrs)
		{
			return use_naked_driver ? RegularCurses.attrset(attrs) : CursesLinux.attrset(attrs);
		}

		public static int getch()
		{
			return use_naked_driver ? RegularCurses.getch() : CursesLinux.getch();
		}

		public static int get_wch(out int sequence)
		{
			return use_naked_driver ? RegularCurses.get_wch(out sequence) : CursesLinux.get_wch(out sequence);
		}

		public static int ungetch(int ch)
		{
			return use_naked_driver ? RegularCurses.ungetch(ch) : CursesLinux.ungetch(ch);
		}

		public static int mvgetch(int y, int x)
		{
			return use_naked_driver ? RegularCurses.mvgetch(y, x) : CursesLinux.mvgetch(y, x);
		}

		public static bool has_colors()
		{
			return use_naked_driver ? RegularCurses.has_colors() : CursesLinux.has_colors();
		}

		public static int start_color()
		{
			return use_naked_driver ? RegularCurses.start_color() : CursesLinux.start_color();
		}

		public static int init_pair(short pair, short f, short b)
		{
			return use_naked_driver ? RegularCurses.init_pair(pair, f, b) : CursesLinux.init_pair(pair, f, b);
		}

		public static int use_default_colors()
		{
			return use_naked_driver ? RegularCurses.use_default_colors() : CursesLinux.use_default_colors();
		}

		public static int COLOR_PAIRS()
		{
			return use_naked_driver ? RegularCurses.COLOR_PAIRS() : CursesLinux.COLOR_PAIRS();
		}

		public static uint getmouse(out MouseEvent ev)
		{
			return use_naked_driver ? RegularCurses.getmouse(out ev) : CursesLinux.getmouse(out ev);
		}

		public static uint ungetmouse(ref MouseEvent ev)
		{
			return use_naked_driver ? RegularCurses.ungetmouse(ref ev) : CursesLinux.ungetmouse(ref ev);
		}

		public static int mouseinterval(int interval)
		{
			return use_naked_driver ? RegularCurses.mouseinterval(interval) : CursesLinux.mouseinterval(interval);
		}

		[StructLayout(LayoutKind.Sequential)]
		internal struct MouseEvent {
			public short ID;

			public int X, Y, Z;

			public Event ButtonState;
		}
	}

	//
	// P/Invoke definitions for looking up symbols in the "ncurses" library, as resolved
	// by the dynamic linker, different than CursesLinux that looksup by "libncursesw.so.5"
	//
	class RegularCurses {
		[DllImport("ncurses", EntryPoint = "initscr")]
		internal static extern IntPtr real_initscr();

		[DllImport("ncurses")]
		public static extern int endwin();

		[DllImport("ncurses")]
		public static extern bool isendwin();

		//
		// Screen operations are flagged as internal, as we need to
		// catch all changes so we can update newscr, curscr, stdscr
		//
		[DllImport("ncurses")]
		public static extern IntPtr internal_newterm(string type, IntPtr file_outfd, IntPtr file_infd);

		[DllImport("ncurses")]
		public static extern IntPtr internal_set_term(IntPtr newscreen);

		[DllImport("ncurses")]
		internal static extern void internal_delscreen(IntPtr sp);

		[DllImport("ncurses")]
		public static extern int cbreak();

		[DllImport("ncurses")]
		public static extern int nocbreak();

		[DllImport("ncurses")]
		public static extern int echo();

		[DllImport("ncurses")]
		public static extern int noecho();

		[DllImport("ncurses")]
		public static extern int halfdelay(int t);

		[DllImport("ncurses")]
		public static extern int raw();

		[DllImport("ncurses")]
		public static extern int noraw();

		[DllImport("ncurses")]
		public static extern void noqiflush();

		[DllImport("ncurses")]
		public static extern void qiflush();

		[DllImport("ncurses")]
		public static extern int typeahead(IntPtr fd);

		[DllImport("ncurses")]
		public static extern int timeout(int delay);

		//
		// Internal, as they are exposed in Window
		//
		[DllImport("ncurses")]
		internal static extern int wtimeout(IntPtr win, int delay);

		[DllImport("ncurses")]
		internal static extern int notimeout(IntPtr win, bool bf);

		[DllImport("ncurses")]
		internal static extern int keypad(IntPtr win, bool bf);

		[DllImport("ncurses")]
		internal static extern int meta(IntPtr win, bool bf);

		[DllImport("ncurses")]
		internal static extern int intrflush(IntPtr win, bool bf);

		[DllImport("ncurses")]
		internal static extern int clearok(IntPtr win, bool bf);

		[DllImport("ncurses")]
		internal static extern int idlok(IntPtr win, bool bf);

		[DllImport("ncurses")]
		internal static extern void idcok(IntPtr win, bool bf);

		[DllImport("ncurses")]
		internal static extern void immedok(IntPtr win, bool bf);

		[DllImport("ncurses")]
		internal static extern int leaveok(IntPtr win, bool bf);

		[DllImport("ncurses")]
		internal static extern int wsetscrreg(IntPtr win, int top, int bot);

		[DllImport("ncurses")]
		internal static extern int scrollok(IntPtr win, bool bf);

		[DllImport("ncurses")]
		public static extern int nl();

		[DllImport("ncurses")]
		public static extern int nonl();

		[DllImport("ncurses")]
		public static extern int setscrreg(int top, int bot);


		[DllImport("ncurses")]
		public static extern int refresh();

		[DllImport("ncurses")]
		public static extern int doupdate();

		[DllImport("ncurses")]
		internal static extern int wrefresh(IntPtr win);

		[DllImport("ncurses")]
		internal static extern int redrawwin(IntPtr win);

		[DllImport("ncurses")]
		internal static extern int wredrawwin(IntPtr win, int beg_line, int num_lines);

		[DllImport("ncurses")]
		internal static extern int wnoutrefresh(IntPtr win);

		[DllImport("ncurses")]
		public static extern int move(int line, int col);

		[DllImport("ncurses", EntryPoint = "addch")]
		internal static extern int _addch(int ch);

		[DllImport("ncurses")]
		public static extern int addstr(string s);

		[DllImport("ncurses")]
		internal static extern int wmove(IntPtr win, int line, int col);

		[DllImport("ncurses")]
		internal static extern int waddch(IntPtr win, int ch);

		[DllImport("ncurses")]
		public static extern int attron(int attrs);

		[DllImport("ncurses")]
		public static extern int attroff(int attrs);

		[DllImport("ncurses")]
		public static extern int attrset(int attrs);

		[DllImport("ncurses")]
		public static extern int getch();

		[DllImport("ncurses")]
		public static extern int get_wch(out int sequence);

		[DllImport("ncurses")]
		public static extern int ungetch(int ch);

		[DllImport("ncurses")]
		public static extern int mvgetch(int y, int x);

		[DllImport("ncurses")]
		internal static extern bool has_colors();

		[DllImport("ncurses")]
		internal static extern int start_color();

		[DllImport("ncurses")]
		internal static extern int init_pair(short pair, short f, short b);

		[DllImport("ncurses")]
		internal static extern int use_default_colors();

		[DllImport("ncurses")]
		internal static extern int COLOR_PAIRS();

		[DllImport("ncurses")]
		public static extern uint getmouse(out Curses.MouseEvent ev);

		[DllImport("ncurses")]
		public static extern uint ungetmouse(ref Curses.MouseEvent ev);

		[DllImport("ncurses")]
		public static extern int mouseinterval(int interval);

		[DllImport("ncurses", EntryPoint = "mousemask")]
		public static extern IntPtr call_mousemask(IntPtr newmask, out IntPtr oldmask);
	}

	//
	// P/Invoke definitions for looking up symbols in the "libncursesw.so.5" library, as resolved
	// by the dynamic linker, different than RegularCurses that looksup by "ncurses"
	//
	class CursesLinux {
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