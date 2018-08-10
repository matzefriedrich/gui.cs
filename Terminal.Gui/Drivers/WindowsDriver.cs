﻿//
// WindowsDriver.cs: Windows specific driver 
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//   Nick Van Dyck (vandyck.nick@outlook.com)
//
// Copyright (c) 2018
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//

namespace Terminal.Gui {
	using System;
	using System.ComponentModel;
	using System.Runtime.InteropServices;
	using System.Threading;
	using System.Threading.Tasks;

	using Mono.Terminal;

	using NStack;

	class WindowsConsole {
		[Flags]
		public enum ButtonState {
			Button1Pressed = 1,

			Button2Pressed = 4,

			Button3Pressed = 8,

			Button4Pressed = 16,

			RightmostButtonPressed = 2
		}

		[Flags]
		public enum ConsoleModes : uint {
			EnableMouseInput = 16,

			EnableQuickEditMode = 64,

			EnableExtendedFlags = 128
		}

		[Flags]
		public enum ControlKeyState {
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
		public enum EventFlags {
			MouseMoved = 1,

			DoubleClick = 2,

			MouseWheeled = 4,

			MouseHorizontalWheeled = 8
		}

		public enum EventType : ushort {
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

		bool ContinueListeningForConsoleEvents = true;

		internal IntPtr InputHandle, OutputHandle;

		readonly uint originalConsoleMode;

		public CharInfo[] OriginalStdOutChars;

		IntPtr ScreenBuffer;

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

		public uint ConsoleMode {
			get {
				uint v;
				GetConsoleMode(this.InputHandle, out v);
				return v;
			}

			set => SetConsoleMode(this.InputHandle, value);
		}

		public uint InputEventCount {
			get {
				uint v;
				GetNumberOfConsoleInputEvents(this.InputHandle, out v);
				return v;
			}
		}

		public bool WriteToConsole(CharInfo[] charInfoBuffer, Coord coords, SmallRect window)
		{
			if (this.ScreenBuffer == IntPtr.Zero) {
				this.ScreenBuffer = CreateConsoleScreenBuffer(
					DesiredAccess.GenericRead | DesiredAccess.GenericWrite,
					ShareMode.FileShareRead | ShareMode.FileShareWrite,
					IntPtr.Zero,
					1,
					IntPtr.Zero
				);
				if (this.ScreenBuffer == INVALID_HANDLE_VALUE) {
					int err = Marshal.GetLastWin32Error();

					if (err != 0)
						throw new Win32Exception(err);
				}

				if (!SetConsoleActiveScreenBuffer(this.ScreenBuffer)) {
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
			if (!SetConsoleActiveScreenBuffer(this.OutputHandle)) {
				int err = Marshal.GetLastWin32Error();
				Console.WriteLine("Error: {0}", err);
			}
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		static extern IntPtr GetStdHandle(int nStdHandle);

		[DllImport("kernel32.dll", EntryPoint = "ReadConsoleInputW", CharSet = CharSet.Unicode)]
		public static extern bool ReadConsoleInput(
			IntPtr hConsoleInput,
			[Out] InputRecord[] lpBuffer,
			uint nLength,
			out uint lpNumberOfEventsRead);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		static extern bool ReadConsoleOutput(
			IntPtr hConsoleOutput,
			[Out] CharInfo[] lpBuffer,
			Coord dwBufferSize,
			Coord dwBufferCoord,
			ref SmallRect lpReadRegion
		);

		[DllImport("kernel32.dll", EntryPoint = "WriteConsoleOutput", SetLastError = true, CharSet = CharSet.Unicode)]
		static extern bool WriteConsoleOutput(
			IntPtr hConsoleOutput,
			CharInfo[] lpBuffer,
			Coord dwBufferSize,
			Coord dwBufferCoord,
			ref SmallRect lpWriteRegion
		);

		[DllImport("kernel32.dll")]
		static extern bool SetConsoleCursorPosition(IntPtr hConsoleOutput, Coord dwCursorPosition);

		[DllImport("kernel32.dll")]
		static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);


		[DllImport("kernel32.dll")]
		static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

		[DllImport("kernel32.dll", SetLastError = true)]
		static extern IntPtr CreateConsoleScreenBuffer(
			DesiredAccess dwDesiredAccess,
			ShareMode dwShareMode,
			IntPtr secutiryAttributes,
			uint flags,
			IntPtr screenBufferData
		);


		[DllImport("kernel32.dll", SetLastError = true)]
		static extern bool SetConsoleActiveScreenBuffer(IntPtr Handle);

		[DllImport("kernel32.dll", SetLastError = true)]
		static extern bool GetNumberOfConsoleInputEvents(IntPtr handle, out uint lpcNumberOfEvents);

		[StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
		public struct KeyEventRecord {
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
		public struct MouseEventRecord {
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
		public struct Coordinate {
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

		internal struct WindowBufferSizeRecord {
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
		public struct MenuEventRecord {
			public uint dwCommandId;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct FocusEventRecord {
			public uint bSetFocus;
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct InputRecord {
			[FieldOffset(0)] public EventType EventType;

			[FieldOffset(4)] public KeyEventRecord KeyEvent;

			[FieldOffset(4)] public MouseEventRecord MouseEvent;

			[FieldOffset(4)] public WindowBufferSizeRecord WindowBufferSizeEvent;

			[FieldOffset(4)] public MenuEventRecord MenuEvent;

			[FieldOffset(4)] public FocusEventRecord FocusEvent;

			public override string ToString()
			{
				switch (this.EventType) {
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
		enum ShareMode : uint {
			FileShareRead = 1,

			FileShareWrite = 2
		}

		[Flags]
		enum DesiredAccess : uint {
			GenericRead = 2147483648,

			GenericWrite = 1073741824
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct ConsoleScreenBufferInfo {
			public Coord dwSize;

			public Coord dwCursorPosition;

			public ushort wAttributes;

			public SmallRect srWindow;

			public Coord dwMaximumWindowSize;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Coord {
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
		public struct CharUnion {
			[FieldOffset(0)] public char UnicodeChar;

			[FieldOffset(0)] public byte AsciiChar;
		}

		[StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
		public struct CharInfo {
			[FieldOffset(0)] public CharUnion Char;

			[FieldOffset(2)] public ushort Attributes;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct SmallRect {
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
				if (rect.Left == -1) {
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

	class WindowsDriver : ConsoleDriver, IMainLoopDriver {
		static bool sync;

		int ccol, crow;

		int cols, rows;

		int currentAttribute;

		WindowsConsole.SmallRect damageRegion;

		readonly AutoResetEvent eventReady = new AutoResetEvent(false);

		Action<KeyEvent> keyHandler;

		WindowsConsole.ButtonState? LastMouseButtonPressed;

		MainLoop mainLoop;

		Action<MouseEvent> mouseHandler;

		WindowsConsole.CharInfo[] OutputBuffer;

		// The records that we keep fetching
		WindowsConsole.InputRecord[] result;

		readonly WindowsConsole.InputRecord[] records = new WindowsConsole.InputRecord [1];

		Action TerminalResized;

		readonly AutoResetEvent waitForProbe = new AutoResetEvent(false);

		readonly WindowsConsole winConsole;

		public WindowsDriver()
		{
			this.winConsole = new WindowsConsole();

			this.cols = Console.WindowWidth;
			this.rows = Console.WindowHeight - 1;
			WindowsConsole.SmallRect.MakeEmpty(ref this.damageRegion);

			this.ResizeScreen();
			this.UpdateOffScreen();

			Task.Run((Action) this.WindowsInputHandler);
		}

		public override int Cols => this.cols;

		public override int Rows => this.rows;

		void IMainLoopDriver.Setup(MainLoop mainLoop)
		{
			this.mainLoop = mainLoop;
		}

		void IMainLoopDriver.Wakeup()
		{
		}

		bool IMainLoopDriver.EventsPending(bool wait)
		{
			long now = DateTime.UtcNow.Ticks;

			int waitTimeout;
			if (this.mainLoop.timeouts.Count > 0) {
				waitTimeout = (int) ((this.mainLoop.timeouts.Keys[0] - now) / TimeSpan.TicksPerMillisecond);
				if (waitTimeout < 0)
					return true;
			} else
				waitTimeout = -1;

			if (!wait)
				waitTimeout = 0;

			this.result = null;
			this.waitForProbe.Set();
			this.eventReady.WaitOne(waitTimeout);
			return this.result != null;
		}


		void IMainLoopDriver.MainIteration()
		{
			if (this.result == null)
				return;

			var inputEvent = this.result[0];
			switch (inputEvent.EventType) {
			case WindowsConsole.EventType.Key:
				if (inputEvent.KeyEvent.bKeyDown == false)
					return;
				var map = this.MapKey(this.ToConsoleKeyInfo(inputEvent.KeyEvent));
				if (inputEvent.KeyEvent.UnicodeChar == 0 && map == (Key) 0xffffffff)
					return;
				this.keyHandler(new KeyEvent(map));
				break;

			case WindowsConsole.EventType.Mouse:
				this.mouseHandler(this.ToDriverMouse(inputEvent.MouseEvent));
				break;

			case WindowsConsole.EventType.WindowBufferSize:
				this.cols = inputEvent.WindowBufferSizeEvent.size.X;
				this.rows = inputEvent.WindowBufferSizeEvent.size.Y - 1;
				this.ResizeScreen();
				this.UpdateOffScreen();
				this.TerminalResized();
				break;
			}

			this.result = null;
		}

		void WindowsInputHandler()
		{
			while (true) {
				this.waitForProbe.WaitOne();

				uint numberEventsRead = 0;

				WindowsConsole.ReadConsoleInput(this.winConsole.InputHandle, this.records, 1, out numberEventsRead);
				if (numberEventsRead == 0)
					this.result = null;
				else
					this.result = this.records;

				this.eventReady.Set();
			}
		}

		public override void PrepareToRun(MainLoop mainLoop, Action<KeyEvent> keyHandler, Action<MouseEvent> mouseHandler)
		{
			this.keyHandler = keyHandler;
			this.mouseHandler = mouseHandler;
		}

		MouseEvent ToDriverMouse(WindowsConsole.MouseEventRecord mouseEvent)
		{
			var mouseFlag = MouseFlags.AllEvents;

			// The ButtonState member of the MouseEvent structure has bit corresponding to each mouse button.
			// This will tell when a mouse button is pressed. When the button is released this event will
			// be fired with it's bit set to 0. So when the button is up ButtonState will be 0.
			// To map to the correct driver events we save the last pressed mouse button so we can
			// map to the correct clicked event.
			if (this.LastMouseButtonPressed != null && mouseEvent.ButtonState != 0)
				this.LastMouseButtonPressed = null;

			if (mouseEvent.EventFlags == 0 && this.LastMouseButtonPressed == null) {
				switch (mouseEvent.ButtonState) {
				case WindowsConsole.ButtonState.Button1Pressed:
					mouseFlag = MouseFlags.Button1Pressed;
					break;

				case WindowsConsole.ButtonState.Button2Pressed:
					mouseFlag = MouseFlags.Button2Pressed;
					break;

				case WindowsConsole.ButtonState.Button3Pressed:
					mouseFlag = MouseFlags.Button3Pressed;
					break;
				}

				this.LastMouseButtonPressed = mouseEvent.ButtonState;
			} else if (mouseEvent.EventFlags == 0 && this.LastMouseButtonPressed != null) {
				switch (this.LastMouseButtonPressed) {
				case WindowsConsole.ButtonState.Button1Pressed:
					mouseFlag = MouseFlags.Button1Clicked;
					break;

				case WindowsConsole.ButtonState.Button2Pressed:
					mouseFlag = MouseFlags.Button2Clicked;
					break;

				case WindowsConsole.ButtonState.Button3Pressed:
					mouseFlag = MouseFlags.Button3Clicked;
					break;
				}

				this.LastMouseButtonPressed = null;
			} else if (mouseEvent.EventFlags == WindowsConsole.EventFlags.MouseMoved)
				mouseFlag = MouseFlags.ReportMousePosition;

			return new MouseEvent {
				X = mouseEvent.MousePosition.X,
				Y = mouseEvent.MousePosition.Y,
				Flags = mouseFlag
			};
		}

		ConsoleKeyInfo ToConsoleKeyInfo(WindowsConsole.KeyEventRecord keyEvent)
		{
			var state = keyEvent.dwControlKeyState;

			bool shift = (state & WindowsConsole.ControlKeyState.ShiftPressed) != 0;
			bool alt = (state & (WindowsConsole.ControlKeyState.LeftAltPressed | WindowsConsole.ControlKeyState.RightAltPressed)) != 0;
			bool control = (state & (WindowsConsole.ControlKeyState.LeftControlPressed | WindowsConsole.ControlKeyState.RightControlPressed)) != 0;

			return new ConsoleKeyInfo(keyEvent.UnicodeChar, (ConsoleKey) keyEvent.wVirtualKeyCode, shift, alt, control);
		}

		public Key MapKey(ConsoleKeyInfo keyInfo)
		{
			switch (keyInfo.Key) {
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
				return Key.DeleteChar;

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

			var key = keyInfo.Key;
			if (key >= ConsoleKey.A && key <= ConsoleKey.Z) {
				int delta = key - ConsoleKey.A;
				if (keyInfo.Modifiers == ConsoleModifiers.Control)
					return (Key) ((uint) Key.ControlA + delta);
				if (keyInfo.Modifiers == ConsoleModifiers.Alt)
					return (Key) ((uint) Key.AltMask | ((uint) 'A' + delta));
				if (keyInfo.Modifiers == ConsoleModifiers.Shift)
					return (Key) ((uint) 'A' + delta);
				return (Key) ((uint) 'a' + delta);
			}

			if (key >= ConsoleKey.D0 && key <= ConsoleKey.D9) {
				int delta = key - ConsoleKey.D0;
				if (keyInfo.Modifiers == ConsoleModifiers.Alt)
					return (Key) ((uint) Key.AltMask | ((uint) '0' + delta));

				return (Key) keyInfo.KeyChar;
			}

			if (key >= ConsoleKey.F1 && key <= ConsoleKey.F10) {
				int delta = key - ConsoleKey.F1;

				return (Key) ((int) Key.F1 + delta);
			}

			return (Key) 0xffffffff;
		}

		public override void Init(Action terminalResized)
		{
			this.TerminalResized = terminalResized;

			Colors.Base = new ColorScheme();
			Colors.Dialog = new ColorScheme();
			Colors.Menu = new ColorScheme();
			Colors.Error = new ColorScheme();

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

			Colors.Base.Normal = this.MakeColor(ConsoleColor.White, ConsoleColor.Blue);
			Colors.Base.Focus = this.MakeColor(ConsoleColor.Black, ConsoleColor.Cyan);
			Colors.Base.HotNormal = this.MakeColor(ConsoleColor.Yellow, ConsoleColor.Blue);
			Colors.Base.HotFocus = this.MakeColor(ConsoleColor.Yellow, ConsoleColor.Cyan);

			Colors.Menu.HotFocus = this.MakeColor(ConsoleColor.Yellow, ConsoleColor.Black);
			Colors.Menu.Focus = this.MakeColor(ConsoleColor.White, ConsoleColor.Black);
			Colors.Menu.HotNormal = this.MakeColor(ConsoleColor.Yellow, ConsoleColor.Cyan);
			Colors.Menu.Normal = this.MakeColor(ConsoleColor.White, ConsoleColor.Cyan);

			Colors.Dialog.Normal = this.MakeColor(ConsoleColor.Black, ConsoleColor.Gray);
			Colors.Dialog.Focus = this.MakeColor(ConsoleColor.Black, ConsoleColor.Cyan);
			Colors.Dialog.HotNormal = this.MakeColor(ConsoleColor.Blue, ConsoleColor.Gray);
			Colors.Dialog.HotFocus = this.MakeColor(ConsoleColor.Blue, ConsoleColor.Cyan);

			Colors.Error.Normal = this.MakeColor(ConsoleColor.White, ConsoleColor.Red);
			Colors.Error.Focus = this.MakeColor(ConsoleColor.Black, ConsoleColor.Gray);
			Colors.Error.HotNormal = this.MakeColor(ConsoleColor.Yellow, ConsoleColor.Red);
			Colors.Error.HotFocus = Colors.Error.HotNormal;
			Console.Clear();
		}

		void ResizeScreen()
		{
			this.OutputBuffer = new WindowsConsole.CharInfo [this.Rows * this.Cols];
			this.Clip = new Rect(0, 0, this.Cols, this.Rows);
			this.damageRegion = new WindowsConsole.SmallRect {
				Top = 0,
				Left = 0,
				Bottom = (short) this.Rows,
				Right = (short) this.Cols
			};
		}

		void UpdateOffScreen()
		{
			for (var row = 0; row < this.rows; row++)
			for (var col = 0; col < this.cols; col++) {
				int position = row * this.cols + col;
				this.OutputBuffer[position].Attributes = (ushort) this.MakeColor(ConsoleColor.White, ConsoleColor.Blue);
				this.OutputBuffer[position].Char.UnicodeChar = ' ';
			}
		}

		public override void Move(int col, int row)
		{
			this.ccol = col;
			this.crow = row;
		}

		public override void AddRune(Rune rune)
		{
			int position = this.crow * this.Cols + this.ccol;

			if (this.Clip.Contains(this.ccol, this.crow)) {
				this.OutputBuffer[position].Attributes = (ushort) this.currentAttribute;
				this.OutputBuffer[position].Char.UnicodeChar = (char) rune;
				WindowsConsole.SmallRect.Update(ref this.damageRegion, (short) this.ccol, (short) this.crow);
			}

			this.ccol++;
			if (this.ccol == this.Cols) {
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

		public override void SetAttribute(Attribute c)
		{
			this.currentAttribute = c.value;
		}

		Attribute MakeColor(ConsoleColor f, ConsoleColor b)
		{
			// Encode the colors into the int value.
			return new Attribute {
				value = (int) f | ((int) b << 4)
			};
		}

		public override void Refresh()
		{
			this.UpdateScreen();
#if false
			var bufferCoords = new WindowsConsole.Coord (){
				X = (short)Clip.Width,
				Y = (short)Clip.Height
			};

			var window = new WindowsConsole.SmallRect (){
				Top = 0,
				Left = 0,
				Right = (short)Clip.Right,
				Bottom = (short)Clip.Bottom
			};

			UpdateCursor();
			winConsole.WriteToConsole (OutputBuffer, bufferCoords, window);
#endif
		}

		public override void UpdateScreen()
		{
			if (this.damageRegion.Left == -1)
				return;

			var bufferCoords = new WindowsConsole.Coord {
				X = (short) this.Clip.Width,
				Y = (short) this.Clip.Height
			};

			var window = new WindowsConsole.SmallRect {
				Top = 0,
				Left = 0,
				Right = (short) this.Clip.Right,
				Bottom = (short) this.Clip.Bottom
			};

			this.UpdateCursor();
			this.winConsole.WriteToConsole(this.OutputBuffer, bufferCoords, this.damageRegion);
//			System.Diagnostics.Debugger.Log(0, "debug", $"Region={damageRegion.Right - damageRegion.Left},{damageRegion.Bottom - damageRegion.Top}\n");
			WindowsConsole.SmallRect.MakeEmpty(ref this.damageRegion);
		}

		public override void UpdateCursor()
		{
			var position = new WindowsConsole.Coord {
				X = (short) this.ccol,
				Y = (short) this.crow
			};
			this.winConsole.SetCursorPosition(position);
		}

		public override void End()
		{
			this.winConsole.Cleanup();
		}

		#region Unused

		public override void SetColors(ConsoleColor foreground, ConsoleColor background)
		{
		}

		public override void SetColors(short foregroundColorId, short backgroundColorId)
		{
		}

		public override void Suspend()
		{
		}

		public override void StartReportingMouseMoves()
		{
		}

		public override void StopReportingMouseMoves()
		{
		}

		public override void UncookMouse()
		{
		}

		public override void CookMouse()
		{
		}

		#endregion
	}
}