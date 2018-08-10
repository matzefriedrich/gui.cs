//
// Label.cs: Label control
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//

namespace Terminal.Gui {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

	using NStack;

	/// <summary>
	///     Text alignment enumeration, controls how text is displayed.
	/// </summary>
	public enum TextAlignment {
		/// <summary>
		///     Aligns the text to the left of the frame.
		/// </summary>
		Left,

		/// <summary>
		///     Aligns the text to the right side of the frame.
		/// </summary>
		Right,

		/// <summary>
		///     Centers the text in the frame.
		/// </summary>
		Centered,

		/// <summary>
		///     Shows the line as justified text in the line.
		/// </summary>
		Justified
	}

	/// <summary>
	///     Label view, displays a string at a given position, can include multiple lines.
	/// </summary>
	public class Label : View {
		static readonly char[] whitespace = {' ', '\t'};

		readonly List<ustring> lines = new List<ustring>();

		bool recalcPending = true;

		ustring text;

		TextAlignment textAlignment;

		Attribute textColor = -1;

		/// <summary>
		///     Public constructor: creates a label at the given
		///     coordinate with the given string, computes the bounding box
		///     based on the size of the string, assumes that the string contains
		///     newlines for multiple lines, no special breaking rules are used.
		/// </summary>
		public Label(int x, int y, ustring text) : this(CalcRect(x, y, text), text)
		{
		}

		/// <summary>
		///     Public constructor: creates a label at the given
		///     coordinate with the given string and uses the specified
		///     frame for the string.
		/// </summary>
		public Label(Rect rect, ustring text) : base(rect)
		{
			this.text = text;
		}

		/// <summary>
		///     Public constructor: creates a label and configures the default Width and Height based on the text, the result is
		///     suitable for Computed layout.
		/// </summary>
		/// <param name="text">Text.</param>
		public Label(ustring text)
		{
			this.text = text;
			var r = CalcRect(0, 0, text);
			this.Width = r.Width;
			this.Height = r.Height;
		}

		/// <summary>
		///     The text displayed by this widget.
		/// </summary>
		public virtual ustring Text {
			get => this.text;
			set {
				this.text = value;
				this.recalcPending = true;
				this.SetNeedsDisplay();
			}
		}

		/// <summary>
		///     Controls the text-alignemtn property of the label, changing it will redisplay the label.
		/// </summary>
		/// <value>The text alignment.</value>
		public TextAlignment TextAlignment {
			get => this.textAlignment;
			set {
				this.textAlignment = value;
				this.SetNeedsDisplay();
			}
		}

		/// <summary>
		///     The color used for the label
		/// </summary>
		public Attribute TextColor {
			get => this.textColor;
			set {
				this.textColor = value;
				this.SetNeedsDisplay();
			}
		}

		static Rect CalcRect(int x, int y, ustring s)
		{
			var mw = 0;
			var ml = 1;

			var cols = 0;
			foreach (uint rune in s)
				if (rune == '\n') {
					ml++;
					if (cols > mw)
						mw = cols;
					cols = 0;
				} else
					cols++;

			return new Rect(x, y, cols, ml);
		}

		static ustring ClipAndJustify(ustring str, int width, TextAlignment talign)
		{
			int slen = str.Length;
			if (slen > width)
				return str[0, width];
			if (talign == TextAlignment.Justified) {
				// TODO: ustring needs this
				var words = str.ToString().Split(whitespace, StringSplitOptions.RemoveEmptyEntries);
				int textCount = words.Sum(arg => arg.Length);

				int spaces = (width - textCount) / (words.Length - 1);
				int extras = (width - textCount) % words.Length;

				var s = new StringBuilder();
				//s.Append ($"tc={textCount} sp={spaces},x={extras} - ");
				for (var w = 0; w < words.Length; w++) {
					string x = words[w];
					s.Append(x);
					if (w + 1 < words.Length)
						for (var i = 0; i < spaces; i++)
							s.Append(' ');
					if (extras > 0) {
						s.Append('_');
						extras--;
					}
				}

				return ustring.Make(s.ToString());
			}

			return str;
		}

		void Recalc()
		{
			this.recalcPending = false;
			Recalc(this.text, this.lines, this.Frame.Width, this.textAlignment);
		}

		static void Recalc(ustring textStr, List<ustring> lineResult, int width, TextAlignment talign)
		{
			lineResult.Clear();
			if (textStr.IndexOf('\n') == -1) {
				lineResult.Add(ClipAndJustify(textStr, width, talign));
				return;
			}

			int textLen = textStr.Length;
			var lp = 0;
			for (var i = 0; i < textLen; i++) {
				Rune c = textStr[i];

				if (c == '\n') {
					lineResult.Add(ClipAndJustify(textStr[lp, i], width, talign));
					lp = i + 1;
				}
			}
		}

		public override void Redraw(Rect region)
		{
			if (this.recalcPending)
				this.Recalc();

			if (this.TextColor != -1)
				Driver.SetAttribute(this.TextColor);
			else
				Driver.SetAttribute(this.ColorScheme.Normal);

			this.Clear();
			this.Move(this.Frame.X, this.Frame.Y);
			for (var line = 0; line < this.lines.Count; line++) {
				if (line < region.Top || line >= region.Bottom)
					continue;
				var str = this.lines[line];
				int x;
				switch (this.textAlignment) {
				case TextAlignment.Left:
				case TextAlignment.Justified:
					x = 0;
					break;
				case TextAlignment.Right:
					x = this.Frame.Right - str.Length;
					break;
				case TextAlignment.Centered:
					x = this.Frame.Left + (this.Frame.Width - str.Length) / 2;
					break;
				default:
					throw new ArgumentOutOfRangeException();
				}

				this.Move(x, line);
				Driver.AddStr(str);
			}
		}

		/// <summary>
		///     Computes the number of lines needed to render the specified text by the Label control
		/// </summary>
		/// <returns>Number of lines.</returns>
		/// <param name="text">Text, may contain newlines.</param>
		/// <param name="width">The width for the text.</param>
		public static int MeasureLines(ustring text, int width)
		{
			var result = new List<ustring>();
			Recalc(text, result, width, TextAlignment.Left);
			return result.Count;
		}
	}
}