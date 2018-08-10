namespace Unix.Terminal
{
    using System;
    using System.Runtime.InteropServices;

    internal class RegularCurses
    {
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
}