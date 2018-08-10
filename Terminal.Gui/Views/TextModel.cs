namespace Terminal.Gui {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;

	using NStack;

	class TextModel {
		List<List<Rune>> lines;

		/// <summary>
		///     The number of text lines in the model
		/// </summary>
		public int Count => this.lines.Count;

		public bool LoadFile(string file)
		{
			if (file == null)
				throw new ArgumentNullException(nameof(file));
			try {
				var stream = File.OpenRead(file);
			} catch {
				return false;
			}

			this.LoadStream(File.OpenRead(file));
			return true;
		}

		// Turns the ustring into runes, this does not split the 
		// contents on a newline if it is present.
		internal static List<Rune> ToRunes(ustring str)
		{
			var runes = new List<Rune>();
			foreach (uint x in str.ToRunes())
				runes.Add(x);
			return runes;
		}

		// Splits a string into a List that contains a List<Rune> for each line
		public static List<List<Rune>> StringToRunes(ustring content)
		{
			var lines = new List<List<Rune>>();
			int start = 0, i = 0;
			for (; i < content.Length; i++)
				if (content[i] == 10) {
					if (i - start > 0)
						lines.Add(ToRunes(content[start, i]));
					else
						lines.Add(ToRunes(ustring.Empty));
					start = i + 1;
				}

			if (i - start >= 0)
				lines.Add(ToRunes(content[start, null]));
			return lines;
		}

		void Append(List<byte> line)
		{
			var str = ustring.Make(line.ToArray());
			this.lines.Add(ToRunes(str));
		}

		public void LoadStream(Stream input)
		{
			if (input == null)
				throw new ArgumentNullException(nameof(input));

			this.lines = new List<List<Rune>>();
			var buff = new BufferedStream(input);
			int v;
			var line = new List<byte>();
			while ((v = buff.ReadByte()) != -1) {
				if (v == 10) {
					this.Append(line);
					line.Clear();
					continue;
				}

				line.Add((byte) v);
			}

			if (line.Count > 0)
				this.Append(line);
		}

		public void LoadString(ustring content)
		{
			this.lines = StringToRunes(content);
		}

		public override string ToString()
		{
			var sb = new StringBuilder();
			foreach (var line in this.lines) {
				sb.Append(line);
				sb.AppendLine();
			}

			return sb.ToString();
		}

		/// <summary>
		///     Returns the specified line as a List of Rune
		/// </summary>
		/// <returns>The line.</returns>
		/// <param name="line">Line number to retrieve.</param>
		public List<Rune> GetLine(int line)
		{
			return this.lines[line];
		}

		/// <summary>
		///     Adds a line to the model at the specified position.
		/// </summary>
		/// <param name="pos">Line number where the line will be inserted.</param>
		/// <param name="runes">The line of text, as a List of Rune.</param>
		public void AddLine(int pos, List<Rune> runes)
		{
			this.lines.Insert(pos, runes);
		}

		/// <summary>
		///     Removes the line at the specified position
		/// </summary>
		/// <param name="pos">Position.</param>
		public void RemoveLine(int pos)
		{
			this.lines.RemoveAt(pos);
		}
	}
}