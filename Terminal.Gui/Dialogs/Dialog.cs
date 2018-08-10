//
// Dialog.cs: Dialog box
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//

namespace Terminal.Gui
{
    using System;
    using System.Collections.Generic;

    using NStack;

    /// <summary>
    ///     The dialog box is a window that by default is centered and contains one
    ///     or more buttons.
    /// </summary>
    public class Dialog : Window
    {
        private const int padding = 1;

        private readonly List<Button> buttons = new List<Button>();

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:Terminal.Gui.Dialog" /> class with an optional set of buttons to
        ///     display
        /// </summary>
        /// <param name="title">Title for the dialog.</param>
        /// <param name="width">Width for the dialog.</param>
        /// <param name="height">Height for the dialog.</param>
        /// <param name="buttons">Optional buttons to lay out at the bottom of the dialog.</param>
        public Dialog(ustring title, int width, int height, params Button[] buttons) : base(title, padding)
        {
            this.X = Pos.Center();
            this.Y = Pos.Center();
            this.Width = width;
            this.Height = height;
            this.ColorScheme = Colors.Dialog;

            if (buttons != null)
                foreach (Button b in buttons)
                {
                    this.buttons.Add(b);
                    this.Add(b);
                }
        }

        /// <summary>
        ///     Adds a button to the dialog, its layout will be controled by the dialog
        /// </summary>
        /// <param name="button">Button to add.</param>
        public void AddButton(Button button)
        {
            if (button == null)
                return;

            this.buttons.Add(button);
            this.Add(button);
        }


        public override void LayoutSubviews()
        {
            base.LayoutSubviews();

            var buttonSpace = 0;
            var maxHeight = 0;

            foreach (Button b in this.buttons)
            {
                buttonSpace += b.Frame.Width + 1;
                maxHeight = Math.Max(maxHeight, b.Frame.Height);
            }

            const int borderWidth = 2;
            int start = (this.Frame.Width - borderWidth - buttonSpace) / 2;

            int y = this.Frame.Height - borderWidth - maxHeight - 1 - padding;
            foreach (Button b in this.buttons)
            {
                Rect bf = b.Frame;

                b.Frame = new Rect(start, y, bf.Width, bf.Height);

                start += bf.Width + 1;
            }
        }

        public override bool ProcessKey(KeyEvent kb)
        {
            switch (kb.Key)
            {
                case Key.Esc:
                    this.Running = false;
                    return true;
            }

            return base.ProcessKey(kb);
        }
    }
}