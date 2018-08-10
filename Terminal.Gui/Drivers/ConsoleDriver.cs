//
// Driver.cs: Definition for the Console Driver API
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//

namespace Terminal.Gui
{
    using System;

    using Mono.Terminal;

    using NStack;

    /// <summary>
    ///     ConsoleDriver is an abstract class that defines the requirements for a console driver.   One implementation if the
    ///     CursesDriver, and another one uses the .NET Console one.
    /// </summary>
    public abstract class ConsoleDriver
    {
        /// <summary>
        ///     The bottom tee.
        /// </summary>
        public Rune BottomTee;

        /// <summary>
        ///     Diamond character
        /// </summary>
        public Rune Diamond;

        /// <summary>
        ///     Horizontal line character.
        /// </summary>
        public Rune HLine;

        /// <summary>
        ///     Left tee
        /// </summary>
        public Rune LeftTee;

        /// <summary>
        ///     Lower left corner
        /// </summary>
        public Rune LLCorner;

        /// <summary>
        ///     Lower right corner
        /// </summary>
        public Rune LRCorner;

        /// <summary>
        ///     Right tee
        /// </summary>
        public Rune RightTee;

        /// <summary>
        ///     Stipple pattern
        /// </summary>
        public Rune Stipple;

        /// <summary>
        ///     Top tee
        /// </summary>
        public Rune TopTee;

        /// <summary>
        ///     Upper left corner
        /// </summary>
        public Rune ULCorner;

        /// <summary>
        ///     Upper right corner
        /// </summary>
        public Rune URCorner;

        /// <summary>
        ///     Vertical line character.
        /// </summary>
        public Rune VLine;

        /// <summary>
        ///     The current number of columns in the terminal.
        /// </summary>
        public abstract int Cols { get; }

        /// <summary>
        ///     The current number of rows in the terminal.
        /// </summary>
        public abstract int Rows { get; }

        /// <summary>
        ///     Controls the current clipping region that AddRune/AddStr is subject to.
        /// </summary>
        /// <value>The clip.</value>
        public Rect Clip { get; set; }

        /// <summary>
        ///     Initializes the driver
        /// </summary>
        /// <param name="terminalResized">Method to invoke when the terminal is resized.</param>
        public abstract void Init(Action terminalResized);

        /// <summary>
        ///     Moves the cursor to the specified column and row.
        /// </summary>
        /// <param name="col">Column to move the cursor to.</param>
        /// <param name="row">Row to move the cursor to.</param>
        public abstract void Move(int col, int row);

        /// <summary>
        ///     Adds the specified rune to the display at the current cursor position
        /// </summary>
        /// <param name="rune">Rune to add.</param>
        public abstract void AddRune(Rune rune);

        /// <summary>
        ///     Adds the specified
        /// </summary>
        /// <param name="str">String.</param>
        public abstract void AddStr(ustring str);

        public abstract void PrepareToRun(MainLoop mainLoop, Action<KeyEvent> keyHandler, Action<MouseEvent> mouseHandler);

        /// <summary>
        ///     Updates the screen to reflect all the changes that have been done to the display buffer
        /// </summary>
        public abstract void Refresh();

        /// <summary>
        ///     Updates the location of the cursor position
        /// </summary>
        public abstract void UpdateCursor();

        /// <summary>
        ///     Ends the execution of the console driver.
        /// </summary>
        public abstract void End();

        /// <summary>
        ///     Redraws the physical screen with the contents that have been queued up via any of the printing commands.
        /// </summary>
        public abstract void UpdateScreen();

        /// <summary>
        ///     Selects the specified attribute as the attribute to use for future calls to AddRune, AddString.
        /// </summary>
        /// <param name="c">C.</param>
        public abstract void SetAttribute(Attribute c);

        // Set Colors from limit sets of colors
        public abstract void SetColors(ConsoleColor foreground, ConsoleColor background);

        // Advanced uses - set colors to any pre-set pairs, you would need to init_color
        // that independently with the R, G, B values.
        /// <summary>
        ///     Advanced uses - set colors to any pre-set pairs, you would need to init_color
        ///     that independently with the R, G, B values.
        /// </summary>
        /// <param name="foregroundColorId">Foreground color identifier.</param>
        /// <param name="backgroundColorId">Background color identifier.</param>
        public abstract void SetColors(short foregroundColorId, short backgroundColorId);

        /// <summary>
        ///     Draws a frame on the specified region with the specified padding around the frame.
        /// </summary>
        /// <param name="region">Region where the frame will be drawn..</param>
        /// <param name="padding">Padding to add on the sides.</param>
        /// <param name="fill">
        ///     If set to <c>true</c> it will clear the contents with the current color, otherwise the contents will
        ///     be left untouched.
        /// </param>
        public virtual void DrawFrame(Rect region, int padding, bool fill)
        {
            int width = region.Width;
            int height = region.Height;
            int b;
            int fwidth = width - padding * 2;
            int fheight = height - 1 - padding;

            this.Move(region.X, region.Y);
            if (padding > 0)
                for (var l = 0; l < padding; l++)
                for (b = 0; b < width; b++)
                    this.AddRune(' ');
            this.Move(region.X, region.Y + padding);
            for (var c = 0; c < padding; c++)
                this.AddRune(' ');
            this.AddRune(this.ULCorner);
            for (b = 0; b < fwidth - 2; b++)
                this.AddRune(this.HLine);
            this.AddRune(this.URCorner);
            for (var c = 0; c < padding; c++)
                this.AddRune(' ');

            for (b = 1 + padding; b < fheight; b++)
            {
                this.Move(region.X, region.Y + b);
                for (var c = 0; c < padding; c++)
                    this.AddRune(' ');
                this.AddRune(this.VLine);
                if (fill)
                    for (var x = 1; x < fwidth - 1; x++)
                        this.AddRune(' ');
                else
                    this.Move(region.X + fwidth - 1, region.Y + b);
                this.AddRune(this.VLine);
                for (var c = 0; c < padding; c++)
                    this.AddRune(' ');
            }

            this.Move(region.X, region.Y + fheight);
            for (var c = 0; c < padding; c++)
                this.AddRune(' ');
            this.AddRune(this.LLCorner);
            for (b = 0; b < fwidth - 2; b++)
                this.AddRune(this.HLine);
            this.AddRune(this.LRCorner);
            for (var c = 0; c < padding; c++)
                this.AddRune(' ');
            if (padding > 0)
            {
                this.Move(region.X, region.Y + height - padding);
                for (var l = 0; l < padding; l++)
                for (b = 0; b < width; b++)
                    this.AddRune(' ');
            }
        }


        /// <summary>
        ///     Suspend the application, typically needs to save the state, suspend the app and upon return, reset the console
        ///     driver.
        /// </summary>
        public abstract void Suspend();

        public abstract void StartReportingMouseMoves();

        public abstract void StopReportingMouseMoves();

        /// <summary>
        ///     Disables the cooked event processing from the mouse driver.  At startup, it is assumed mouse events are cooked.
        /// </summary>
        public abstract void UncookMouse();

        /// <summary>
        ///     Enables the cooked event processing from the mouse driver
        /// </summary>
        public abstract void CookMouse();
    }
}