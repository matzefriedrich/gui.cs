namespace Terminal.Gui {
	using System;

	/// <summary>
	///     Mouse flags reported in MouseEvent.
	/// </summary>
	/// <remarks>
	///     They just happen to map to the ncurses ones.
	/// </remarks>
	[Flags]
	public enum MouseFlags {
		/// <summary>
		///     The first mouse button was pressed.
		/// </summary>
		Button1Pressed = 0x2,

		/// <summary>
		///     The first mouse button was released.
		/// </summary>
		Button1Released = 0x1,

		/// <summary>
		///     The first mouse button was clicked (press+release).
		/// </summary>
		Button1Clicked = 0x4,

		/// <summary>
		///     The first mouse button was double-clicked.
		/// </summary>
		Button1DoubleClicked = 0x8,

		/// <summary>
		///     The first mouse button was tripple-clicked.
		/// </summary>
		Button1TripleClicked = 0x10,

		/// <summary>
		///     The second mouse button was pressed.
		/// </summary>
		Button2Pressed = 0x80,

		/// <summary>
		///     The second mouse button was released.
		/// </summary>
		Button2Released = 0x40,

		/// <summary>
		///     The second mouse button was clicked (press+release).
		/// </summary>
		Button2Clicked = 0x100,

		/// <summary>
		///     The second mouse button was double-clicked.
		/// </summary>
		Button2DoubleClicked = 0x200,

		/// <summary>
		///     The second mouse button was tripple-clicked.
		/// </summary>
		Button2TrippleClicked = 0x400,

		/// <summary>
		///     The third mouse button was pressed.
		/// </summary>
		Button3Pressed = 0x2000,

		/// <summary>
		///     The third mouse button was released.
		/// </summary>
		Button3Released = 0x1000,

		/// <summary>
		///     The third mouse button was clicked (press+release).
		/// </summary>
		Button3Clicked = 0x4000,

		/// <summary>
		///     The third mouse button was double-clicked.
		/// </summary>
		Button3DoubleClicked = 0x8000,

		/// <summary>
		///     The third mouse button was tripple-clicked.
		/// </summary>
		Button3TripleClicked = 0x10000,

		/// <summary>
		///     The fourth mouse button was pressed.
		/// </summary>
		Button4Pressed = 0x80000,

		/// <summary>
		///     The fourth mouse button was released.
		/// </summary>
		Button4Released = 0x40000,

		/// <summary>
		///     The fourth button was clicked (press+release).
		/// </summary>
		Button4Clicked = 0x100000,

		/// <summary>
		///     The fourth button was double-clicked.
		/// </summary>
		Button4DoubleClicked = 0x200000,

		/// <summary>
		///     The fourth button was tripple-clicked.
		/// </summary>
		Button4TripleClicked = 0x400000,

		/// <summary>
		///     The fourth button was pressed.
		/// </summary>
		ButtonShift = 0x2000000,

		/// <summary>
		///     Flag: the shift key was pressed when the mouse button took place.
		/// </summary>
		ButtonCtrl = 0x1000000,

		/// <summary>
		///     Flag: the alt key was pressed when the mouse button took place.
		/// </summary>
		ButtonAlt = 0x4000000,

		/// <summary>
		///     The mouse position is being reported in this event.
		/// </summary>
		ReportMousePosition = 0x8000000,

		/// <summary>
		///     Mask that captures all the events.
		/// </summary>
		AllEvents = 0x7ffffff
	}
}