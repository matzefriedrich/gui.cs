namespace Terminal.Gui {
	/// <summary>
	///     Attributes are used as elements that contain both a foreground and a background or platform specific features
	/// </summary>
	/// <remarks>
	///     Attributes are needed to map colors to terminal capabilities that might lack colors, on color
	///     scenarios, they encode both the foreground and the background color and are used in the ColorScheme
	///     class to define color schemes that can be used in your application.
	/// </remarks>
	public struct Attribute {
		internal int value;

		/// <summary>
		///     Initializes a new instance of the <see cref="T:Terminal.Gui.Attribute" /> struct.
		/// </summary>
		/// <param name="value">Value.</param>
		public Attribute(int value)
		{
			this.value = value;
		}

		public static implicit operator int(Attribute c)
		{
			return c.value;
		}

		public static implicit operator Attribute(int v)
		{
			return new Attribute(v);
		}
	}
}