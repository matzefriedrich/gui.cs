//
// Evemts.cs: Events, Key mappings
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//

namespace Terminal.Gui
{
    /// <summary>
    ///     Describes a mouse event
    /// </summary>
    public struct MouseEvent
    {
        /// <summary>
        ///     The X (column) location for the mouse event.
        /// </summary>
        public int X;

        /// <summary>
        ///     The Y (column) location for the mouse event.
        /// </summary>
        public int Y;

        /// <summary>
        ///     Flags indicating the kind of mouse event that is being posted.
        /// </summary>
        public MouseFlags Flags;

        /// <summary>
        ///     Returns a <see cref="T:System.String" /> that represents the current <see cref="T:Terminal.Gui.MouseEvent" />.
        /// </summary>
        /// <returns>A <see cref="T:System.String" /> that represents the current <see cref="T:Terminal.Gui.MouseEvent" />.</returns>
        public override string ToString()
        {
            return $"({this.X},{this.Y}:{this.Flags}";
        }
    }
}