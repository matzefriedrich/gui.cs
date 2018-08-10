//
// ListView.cs: ListView control
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
// 
// TODO:
//   - Should we support multiple columns, if so, how should that be done?
//   - Show mark for items that have been marked.
//   - Mouse support
//   - Scrollbars?
//
// Column considerations:
//   - Would need a way to specify widths
//   - Should it automatically extract data out of structs/classes based on public fields/properties?
//   - It seems that this would be useful just for the "simple" API, not the IListDAtaSource, as that one has full support for it.
//   - Should a function be specified that retrieves the individual elements?   
// 

namespace Terminal.Gui {
	using System;
	using System.Collections;

	using NStack;

	/// <summary>
	///     Implement this interface to provide your own custom rendering for a list.
	/// </summary>
	public interface IListDataSource {
		/// <summary>
		///     Returns the number of elements to display
		/// </summary>
		int Count { get; }

		/// <summary>
		///     This method is invoked to render a specified item, the method should cover the entire provided width.
		/// </summary>
		/// <returns>The render.</returns>
		/// <param name="selected">Describes whether the item being rendered is currently selected by the user.</param>
		/// <param name="item">The index of the item to render, zero for the first item and so on.</param>
		/// <param name="col">The column where the rendering will start</param>
		/// <param name="line">The line where the rendering will be done.</param>
		/// <param name="width">The width that must be filled out.</param>
		/// <remarks>
		///     The default color will be set before this method is invoked, and will be based on whether the item is selected or
		///     not.
		/// </remarks>
		void Render(bool selected, int item, int col, int line, int width);

		/// <summary>
		///     Should return whether the specified item is currently marked.
		/// </summary>
		/// <returns><c>true</c>, if marked, <c>false</c> otherwise.</returns>
		/// <param name="item">Item index.</param>
		bool IsMarked(int item);

		/// <summary>
		///     Flags the item as marked.
		/// </summary>
		/// <param name="item">Item index.</param>
		/// <param name="value">If set to <c>true</c> value.</param>
		void SetMark(int item, bool value);
	}

	/// <summary>
	///     ListView widget renders a list of data.
	/// </summary>
	/// <remarks>
	///     <para>
	///         The ListView displays lists of data and allows the user to scroll through the data
	///         and optionally mark elements of the list (controlled by the AllowsMark property).
	///     </para>
	///     <para>
	///         The ListView can either render an arbitrary IList object (for example, arrays, List&lt;T&gt;
	///         and other collections) which are drawn by drawing the string/ustring contents or the
	///         result of calling ToString().   Alternatively, you can provide you own IListDataSource
	///         object that gives you full control of what is rendered.
	///     </para>
	///     <para>
	///         The ListView can display any object that implements the System.Collection.IList interface,
	///         string values are converted into ustring values before rendering, and other values are
	///         converted into ustrings by calling ToString() and then converting to ustring.
	///     </para>
	///     <para>
	///         If you must change the contents of the ListView, set the Source property (when you are
	///         providing your own rendering via the IListDataSource implementation) or call SetSource
	///         when you are providing an IList.
	///     </para>
	/// </remarks>
	public class ListView : View {
		bool allowsMarking;

		int selected;

		IListDataSource source;

		int top;

		/// <summary>
		///     Initializes a new ListView that will display the contents of the object implementing the IList interface, with
		///     relative positioning
		/// </summary>
		/// <param name="source">
		///     An IList data source, if the elements of the IList are strings or ustrings, the string is
		///     rendered, otherwise the ToString() method is invoked on the result.
		/// </param>
		public ListView(IList source) : this(MakeWrapper(source))
		{
			((ListWrapper) this.Source).Container = this;
			((ListWrapper) this.Source).Driver = Driver;
		}

		/// <summary>
		///     Initializes a new ListView that will display the provided data source, uses relative positioning.
		/// </summary>
		/// <param name="source">
		///     IListDataSource object that provides a mechanism to render the data. The number of elements on the
		///     collection should not change, if you must change, set the "Source" property to reset the internal settings of the
		///     ListView.
		/// </param>
		public ListView(IListDataSource source)
		{
			this.Source = source;
			this.CanFocus = true;
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="T:Terminal.Gui.ListView" /> class.   You must set the Source property
		///     for this to show something.
		/// </summary>
		public ListView()
		{
		}

		/// <summary>
		///     Initializes a new ListView that will display the contents of the object implementing the IList interface with an
		///     absolute position.
		/// </summary>
		/// <param name="rect">Frame for the listview.</param>
		/// <param name="source">
		///     An IList data source, if the elements of the IList are strings or ustrings, the string is
		///     rendered, otherwise the ToString() method is invoked on the result.
		/// </param>
		public ListView(Rect rect, IList source) : this(rect, MakeWrapper(source))
		{
			((ListWrapper) this.Source).Container = this;
			((ListWrapper) this.Source).Driver = Driver;
		}

		/// <summary>
		///     Initializes a new ListView that will display the provided data source  with an absolute position
		/// </summary>
		/// <param name="rect">Frame for the listview.</param>
		/// <param name="source">
		///     IListDataSource object that provides a mechanism to render the data. The number of elements on the
		///     collection should not change, if you must change, set the "Source" property to reset the internal settings of the
		///     ListView.
		/// </param>
		public ListView(Rect rect, IListDataSource source) : base(rect)
		{
			this.Source = source;
			this.CanFocus = true;
		}

		/// <summary>
		///     Gets or sets the IListDataSource backing this view, use SetSource() if you want to set a new IList source.
		/// </summary>
		/// <value>The source.</value>
		public IListDataSource Source {
			get => this.source;
			set {
				this.source = value;
				this.top = 0;
				this.selected = 0;
				this.SetNeedsDisplay();
			}
		}

		/// <summary>
		///     Gets or sets a value indicating whether this <see cref="T:Terminal.Gui.ListView" /> allows items to be marked.
		/// </summary>
		/// <value><c>true</c> if allows marking elements of the list; otherwise, <c>false</c>.</value>
		public bool AllowsMarking {
			get => this.allowsMarking;
			set {
				this.allowsMarking = value;
				this.SetNeedsDisplay();
			}
		}

		/// <summary>
		///     Gets or sets the item that is displayed at the top of the listview
		/// </summary>
		/// <value>The top item.</value>
		public int TopItem {
			get => this.top;
			set {
				if (this.source == null)
					return;

				if (this.top < 0 || this.top >= this.source.Count)
					throw new ArgumentException("value");
				this.top = value;
				this.SetNeedsDisplay();
			}
		}

		/// <summary>
		///     Gets or sets the currently selecteded item.
		/// </summary>
		/// <value>The selected item.</value>
		public int SelectedItem {
			get => this.selected;
			set {
				if (this.source == null)
					return;
				if (this.selected < 0 || this.selected >= this.source.Count)
					throw new ArgumentException("value");
				this.selected = value;
				if (this.selected < this.top)
					this.top = this.selected;
				else if (this.selected >= this.top + this.Frame.Height)
					this.top = this.selected;
			}
		}

		/// <summary>
		///     Sets the source to an IList value, if you want to set a full IListDataSource, use the Source property.
		/// </summary>
		/// <value>An item implementing the IList interface.</value>
		public void SetSource(IList source)
		{
			if (source == null)
				this.Source = null;
			else {
				this.Source = MakeWrapper(source);
				((ListWrapper) this.Source).Container = this;
				((ListWrapper) this.Source).Driver = Driver;
			}
		}


		static IListDataSource MakeWrapper(IList source)
		{
			return new ListWrapper(source);
		}

		/// <summary>
		///     Redraws the ListView
		/// </summary>
		/// <param name="region">Region.</param>
		public override void Redraw(Rect region)
		{
			var current = this.ColorScheme.Focus;
			Driver.SetAttribute(current);
			this.Move(0, 0);
			var f = this.Frame;
			int item = this.top;
			bool focused = this.HasFocus;

			for (var row = 0; row < f.Height; row++, item++) {
				bool isSelected = item == this.selected;

				var newcolor = focused ? (isSelected ? this.ColorScheme.Focus : this.ColorScheme.Normal) : this.ColorScheme.Normal;
				if (newcolor != current) {
					Driver.SetAttribute(newcolor);
					current = newcolor;
				}

				if (this.source == null || item >= this.source.Count) {
					this.Move(0, row);
					for (var c = 0; c < f.Width; c++)
						Driver.AddRune(' ');
				} else
					this.Source.Render(isSelected, item, 0, row, f.Width);
			}
		}

		/// <summary>
		///     This event is raised when the cursor selection has changed.
		/// </summary>
		public event Action SelectedChanged;

		/// <summary>
		///     Handles cursor movement for this view, passes all other events.
		/// </summary>
		/// <returns><c>true</c>, if key was processed, <c>false</c> otherwise.</returns>
		/// <param name="kb">Keyboard event.</param>
		public override bool ProcessKey(KeyEvent kb)
		{
			if (this.source == null)
				return base.ProcessKey(kb);

			switch (kb.Key) {
			case Key.CursorUp:
			case Key.ControlP:
				if (this.selected > 0) {
					this.selected--;
					if (this.selected < this.top)
						this.top = this.selected;
					if (this.SelectedChanged != null)
						this.SelectedChanged();
					this.SetNeedsDisplay();
				}

				return true;

			case Key.CursorDown:
			case Key.ControlN:
				if (this.selected + 1 < this.source.Count) {
					this.selected++;
					if (this.selected >= this.top + this.Frame.Height)
						this.top++;
					if (this.SelectedChanged != null)
						this.SelectedChanged();
					this.SetNeedsDisplay();
				}

				return true;

			case Key.ControlV:
			case Key.PageDown:
				int n = this.selected + this.Frame.Height;
				if (n > this.source.Count)
					n = this.source.Count - 1;
				if (n != this.selected) {
					this.selected = n;
					if (this.source.Count >= this.Frame.Height)
						this.top = this.selected;
					else
						this.top = 0;
					if (this.SelectedChanged != null)
						this.SelectedChanged();
					this.SetNeedsDisplay();
				}

				return true;

			case Key.PageUp:
				n = this.selected - this.Frame.Height;
				if (n < 0)
					n = 0;
				if (n != this.selected) {
					this.selected = n;
					this.top = this.selected;
					if (this.SelectedChanged != null)
						this.SelectedChanged();
					this.SetNeedsDisplay();
				}

				return true;
			}

			return base.ProcessKey(kb);
		}

		/// <summary>
		///     Positions the cursor in this view
		/// </summary>
		public override void PositionCursor()
		{
			this.Move(0, this.selected - this.top);
		}

		public override bool MouseEvent(MouseEvent me)
		{
			if (!me.Flags.HasFlag(MouseFlags.Button1Clicked))
				return false;

			if (!this.HasFocus)
				this.SuperView.SetFocus(this);

			if (this.source == null)
				return false;

			if (me.Y + this.top >= this.source.Count)
				return true;

			this.selected = this.top + me.Y;
			if (this.SelectedChanged != null)
				this.SelectedChanged();
			this.SetNeedsDisplay();
			return true;
		}

		//
		// This class is the built-in IListDataSource that renders arbitrary
		// IList instances
		//
		class ListWrapper : IListDataSource {
			public ListView Container;

			readonly int count;

			public ConsoleDriver Driver;

			readonly BitArray marks;

			readonly IList src;

			public ListWrapper(IList source)
			{
				this.count = source.Count;
				this.marks = new BitArray(this.count);
				this.src = source;
			}

			public int Count => this.src.Count;

			public void Render(bool marked, int item, int col, int line, int width)
			{
				this.Container.Move(col, line);
				var t = this.src[item];
				if (t is ustring)
					this.RenderUstr(t as ustring, col, line, width);
				else if (t is string)
					this.RenderUstr(t as string, col, line, width);
				else
					this.RenderUstr(t.ToString(), col, line, width);
			}

			public bool IsMarked(int item)
			{
				if (item >= 0 && item < this.count)
					return this.marks[item];
				return false;
			}

			public void SetMark(int item, bool value)
			{
				if (item >= 0 && item < this.count)
					this.marks[item] = value;
			}

			void RenderUstr(ustring ustr, int col, int line, int width)
			{
				int byteLen = ustr.Length;
				var used = 0;
				for (var i = 0; i < byteLen;) {
					(uint rune, int size) = Utf8.DecodeRune(ustr, i, i - byteLen);
					int count = Rune.ColumnWidth(rune);
					if (used + count >= width)
						break;
					this.Driver.AddRune(rune);
					used += count;
					i += size;
				}

				for (; used < width; used++)
					this.Driver.AddRune(' ');
			}
		}
	}
}