namespace Terminal.Gui.Drivers
{
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;

    internal class WindowsConsole
    {
        [Flags]
        public enum ButtonState
        {
            Button1Pressed = 1,

            Button2Pressed = 4,

            Button3Pressed = 8,

            Button4Pressed = 16,

            RightmostButtonPressed = 2
        }

        [Flags]
        public enum ConsoleModes : uint
        {
            EnableMouseInput = 16,

            EnableQuickEditMode = 64,

            EnableExtendedFlags = 128
        }

        [Flags]
        public enum ControlKeyState
        {
            RightAltPressed = 1,

            LeftAltPressed = 2,

            RightControlPressed = 4,

            LeftControlPressed = 8,

            ShiftPressed = 16,

            NumlockOn = 32,

            ScrolllockOn = 64,

            CapslockOn = 128,

            EnhancedKey = 256
        }

        [Flags]
        public enum EventFlags
        {
            MouseMoved = 1,

            DoubleClick = 2,

            MouseWheeled = 4,

            MouseHorizontalWheeled = 8
        }

        public enum EventType : ushort
        {
            Focus = 0x10,

            Key = 0x1,

            Menu = 0x8,

            Mouse = 2,

            WindowBufferSize = 4
        }

        public const int STD_OUTPUT_HANDLE = -11;

        public const int STD_INPUT_HANDLE = -10;

        public const int STD_ERROR_HANDLE = -12;

        internal static IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        private readonly uint originalConsoleMode;

        private bool ContinueListeningForConsoleEvents = true;

        internal IntPtr InputHandle, OutputHandle;

        public CharInfo[] OriginalStdOutChars;

        private IntPtr ScreenBuffer;

        public WindowsConsole()
        {
            this.InputHandle = GetStdHandle(STD_INPUT_HANDLE);
            this.OutputHandle = GetStdHandle(STD_OUTPUT_HANDLE);
            this.originalConsoleMode = this.ConsoleMode;
            uint newConsoleMode = this.originalConsoleMode;
            newConsoleMode |= (uint) (ConsoleModes.EnableMouseInput | ConsoleModes.EnableExtendedFlags);
            newConsoleMode &= ~(uint) ConsoleModes.EnableQuickEditMode;
            this.ConsoleMode = newConsoleMode;
        }

        public uint ConsoleMode
        {
            get
            {
                uint v;
                GetConsoleMode(this.InputHandle, out v);
                return v;
            }

            set => SetConsoleMode(this.InputHandle, value);
        }

        public uint InputEventCount
        {
            get
            {
                uint v;
                GetNumberOfConsoleInputEvents(this.InputHandle, out v);
                return v;
            }
        }

        public bool WriteToConsole(CharInfo[] charInfoBuffer, Coord coords, SmallRect window)
        {
            if (this.ScreenBuffer == IntPtr.Zero)
            {
                this.ScreenBuffer = CreateConsoleScreenBuffer(
                    DesiredAccess.GenericRead | DesiredAccess.GenericWrite,
                    ShareMode.FileShareRead | ShareMode.FileShareWrite,
                    IntPtr.Zero,
                    1,
                    IntPtr.Zero
                );
                if (this.ScreenBuffer == INVALID_HANDLE_VALUE)
                {
                    int err = Marshal.GetLastWin32Error();

                    if (err != 0)
                        throw new Win32Exception(err);
                }

                if (!SetConsoleActiveScreenBuffer(this.ScreenBuffer))
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new Win32Exception(err);
                }

                this.OriginalStdOutChars = new CharInfo [Console.WindowHeight * Console.WindowWidth];

                ReadConsoleOutput(this.OutputHandle, this.OriginalStdOutChars, coords, new Coord {X = 0, Y = 0}, ref window);
            }

            return WriteConsoleOutput(this.ScreenBuffer, charInfoBuffer, coords, new Coord {X = window.Left, Y = window.Top}, ref window);
        }

        public bool SetCursorPosition(Coord position)
        {
            return SetConsoleCursorPosition(this.ScreenBuffer, position);
        }

        public void Cleanup()
        {
            this.ConsoleMode = this.originalConsoleMode;
            this.ContinueListeningForConsoleEvents = false;
            if (!SetConsoleActiveScreenBuffer(this.OutputHandle))
            {
                int err = Marshal.GetLastWin32Error();
                Console.WriteLine("Error: {0}", err);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", EntryPoint = "ReadConsoleInputW", CharSet = CharSet.Unicode)]
        public static extern bool ReadConsoleInput(
            IntPtr hConsoleInput,
            [Out] InputRecord[] lpBuffer,
            uint nLength,
            out uint lpNumberOfEventsRead);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ReadConsoleOutput(
            IntPtr hConsoleOutput,
            [Out] CharInfo[] lpBuffer,
            Coord dwBufferSize,
            Coord dwBufferCoord,
            ref SmallRect lpReadRegion
        );

        [DllImport("kernel32.dll", EntryPoint = "WriteConsoleOutput", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool WriteConsoleOutput(
            IntPtr hConsoleOutput,
            CharInfo[] lpBuffer,
            Coord dwBufferSize,
            Coord dwBufferCoord,
            ref SmallRect lpWriteRegion
        );

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleCursorPosition(IntPtr hConsoleOutput, Coord dwCursorPosition);

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);


        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateConsoleScreenBuffer(
            DesiredAccess dwDesiredAccess,
            ShareMode dwShareMode,
            IntPtr secutiryAttributes,
            uint flags,
            IntPtr screenBufferData
        );


        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleActiveScreenBuffer(IntPtr Handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetNumberOfConsoleInputEvents(IntPtr handle, out uint lpcNumberOfEvents);

        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
        public struct KeyEventRecord
        {
            [FieldOffset(0)] [MarshalAs(UnmanagedType.Bool)]
            public bool bKeyDown;

            [FieldOffset(4)] [MarshalAs(UnmanagedType.U2)]
            public ushort wRepeatCount;

            [FieldOffset(6)] [MarshalAs(UnmanagedType.U2)]
            public ushort wVirtualKeyCode;

            [FieldOffset(8)] [MarshalAs(UnmanagedType.U2)]
            public ushort wVirtualScanCode;

            [FieldOffset(10)] public char UnicodeChar;

            [FieldOffset(12)] [MarshalAs(UnmanagedType.U4)]
            public ControlKeyState dwControlKeyState;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct MouseEventRecord
        {
            [FieldOffset(0)] public Coordinate MousePosition;

            [FieldOffset(4)] public ButtonState ButtonState;

            [FieldOffset(8)] public ControlKeyState ControlKeyState;

            [FieldOffset(12)] public EventFlags EventFlags;

            public override string ToString()
            {
                return $"[Mouse({this.MousePosition},{this.ButtonState},{this.ControlKeyState},{this.EventFlags}";
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Coordinate
        {
            public short X;

            public short Y;

            public Coordinate(short X, short Y)
            {
                this.X = X;
                this.Y = Y;
            }

            public override string ToString()
            {
                return $"({this.X},{this.Y})";
            }
        }

        internal struct WindowBufferSizeRecord
        {
            public Coordinate size;

            public WindowBufferSizeRecord(short x, short y)
            {
                this.size = new Coordinate(x, y);
            }

            public override string ToString()
            {
                return $"[WindowBufferSize{this.size}";
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MenuEventRecord
        {
            public uint dwCommandId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FocusEventRecord
        {
            public uint bSetFocus;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputRecord
        {
            [FieldOffset(0)] public EventType EventType;

            [FieldOffset(4)] public KeyEventRecord KeyEvent;

            [FieldOffset(4)] public MouseEventRecord MouseEvent;

            [FieldOffset(4)] public WindowBufferSizeRecord WindowBufferSizeEvent;

            [FieldOffset(4)] public MenuEventRecord MenuEvent;

            [FieldOffset(4)] public FocusEventRecord FocusEvent;

            public override string ToString()
            {
                switch (this.EventType)
                {
                    case EventType.Focus:
                        return this.FocusEvent.ToString();
                    case EventType.Key:
                        return this.KeyEvent.ToString();
                    case EventType.Menu:
                        return this.MenuEvent.ToString();
                    case EventType.Mouse:
                        return this.MouseEvent.ToString();
                    case EventType.WindowBufferSize:
                        return this.WindowBufferSizeEvent.ToString();
                    default:
                        return "Unknown event type: " + this.EventType;
                }
            }
        }

        [Flags]
        private enum ShareMode : uint
        {
            FileShareRead = 1,

            FileShareWrite = 2
        }

        [Flags]
        private enum DesiredAccess : uint
        {
            GenericRead = 2147483648,

            GenericWrite = 1073741824
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ConsoleScreenBufferInfo
        {
            public Coord dwSize;

            public Coord dwCursorPosition;

            public ushort wAttributes;

            public SmallRect srWindow;

            public Coord dwMaximumWindowSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Coord
        {
            public short X;

            public short Y;

            public Coord(short X, short Y)
            {
                this.X = X;
                this.Y = Y;
            }

            public override string ToString()
            {
                return $"({this.X},{this.Y})";
            }
        }

        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
        public struct CharUnion
        {
            [FieldOffset(0)] public char UnicodeChar;

            [FieldOffset(0)] public byte AsciiChar;
        }

        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
        public struct CharInfo
        {
            [FieldOffset(0)] public CharUnion Char;

            [FieldOffset(2)] public ushort Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SmallRect
        {
            public short Left;

            public short Top;

            public short Right;

            public short Bottom;

            public static void MakeEmpty(ref SmallRect rect)
            {
                rect.Left = -1;
            }

            public static void Update(ref SmallRect rect, short col, short row)
            {
                if (rect.Left == -1)
                {
                    //System.Diagnostics.Debugger.Log (0, "debug", $"damager From Empty {col},{row}\n");
                    rect.Left = rect.Right = col;
                    rect.Bottom = rect.Top = row;
                    return;
                }

                if (col >= rect.Left && col <= rect.Right && row >= rect.Top && row <= rect.Bottom)
                    return;
                if (col < rect.Left)
                    rect.Left = col;
                if (col > rect.Right)
                    rect.Right = col;
                if (row < rect.Top)
                    rect.Top = row;
                if (row > rect.Bottom)
                    rect.Bottom = row;
                //System.Diagnostics.Debugger.Log (0, "debug", $"Expanding {rect.ToString ()}\n");
            }

            public override string ToString()
            {
                return $"Left={this.Left},Top={this.Top},Right={this.Right},Bottom={this.Bottom}";
            }
        }
    }
}