namespace Terminal.Gui {
	/// <summary>
	///     Progress bar can indicate progress of an activity visually.
	/// </summary>
	/// <remarks>
	///     <para>
	///         The progressbar can operate in two modes, percentage mode, or
	///         activity mode.  The progress bar starts in percentage mode and
	///         setting the Fraction property will reflect on the UI the progress
	///         made so far.   Activity mode is used when the application has no
	///         way of knowing how much time is left, and is started when you invoke
	///         the Pulse() method.    You should call the Pulse method repeatedly as
	///         your application makes progress.
	///     </para>
	/// </remarks>
	public class ProgressBar : View {
		int activityPos, delta;

		float fraction;

		bool isActivity;

		/// <summary>
		///     Initializes a new instance of the <see cref="T:Terminal.Gui.ProgressBar" /> class, starts in percentage mode with
		///     an absolute position and size.
		/// </summary>
		/// <param name="rect">Rect.</param>
		public ProgressBar(Rect rect) : base(rect)
		{
			this.CanFocus = false;
			this.fraction = 0;
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="T:Terminal.Gui.ProgressBar" /> class, starts in percentage mode and
		///     uses relative layout.
		/// </summary>
		public ProgressBar()
		{
			this.CanFocus = false;
			this.fraction = 0;
		}

		/// <summary>
		///     Gets or sets the progress indicator fraction to display, must be a value between 0 and 1.
		/// </summary>
		/// <value>The fraction representing the progress.</value>
		public float Fraction {
			get => this.fraction;
			set {
				this.fraction = value;
				this.isActivity = false;
				this.SetNeedsDisplay();
			}
		}

		/// <summary>
		///     Notifies the progress bar that some progress has taken place.
		/// </summary>
		/// <remarks>
		///     If the ProgressBar is is percentage mode, it switches to activity
		///     mode.   If is in activity mode, the marker is moved.
		/// </remarks>
		public void Pulse()
		{
			if (!this.isActivity) {
				this.isActivity = true;
				this.activityPos = 0;
				this.delta = 1;
			} else {
				this.activityPos += this.delta;
				if (this.activityPos < 0) {
					this.activityPos = 1;
					this.delta = 1;
				} else if (this.activityPos >= this.Frame.Width) {
					this.activityPos = this.Frame.Width - 2;
					this.delta = -1;
				}
			}

			this.SetNeedsDisplay();
		}

		public override void Redraw(Rect region)
		{
			Driver.SetAttribute(this.ColorScheme.Normal);

			int top = this.Frame.Width;
			if (this.isActivity) {
				this.Move(0, 0);
				for (var i = 0; i < top; i++)
					if (i == this.activityPos)
						Driver.AddRune(Driver.Stipple);
					else
						Driver.AddRune(' ');
			} else {
				this.Move(0, 0);
				var mid = (int) (this.fraction * top);
				int i;
				for (i = 0; i < mid; i++)
					Driver.AddRune(Driver.Stipple);
				for (; i < top; i++)
					Driver.AddRune(' ');
			}
		}
	}
}