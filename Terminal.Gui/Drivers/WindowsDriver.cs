//
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
	using System.Threading;
	using System.Threading.Tasks;

	using Mono.Terminal;

	using NStack;

	class WindowsDriver : ConsoleDriver, IMainLoopDriver {
		static bool sync;

		readonly AutoResetEvent eventReady = new AutoResetEvent(false);

		readonly WindowsConsole.InputRecord[] records = new WindowsConsole.InputRecord [1];

		readonly AutoResetEvent waitForProbe = new AutoResetEvent(false);

		readonly WindowsConsole winConsole;

		int ccol, crow;

		int cols, rows;

		int currentAttribute;

		WindowsConsole.SmallRect damageRegion;

		Action<KeyEvent> keyHandler;

		WindowsConsole.ButtonState? LastMouseButtonPressed;

		MainLoop mainLoop;

		Action<MouseEvent> mouseHandler;

		WindowsConsole.CharInfo[] OutputBuffer;

		// The records that we keep fetching
		WindowsConsole.InputRecord[] result;

		Action TerminalResized;

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