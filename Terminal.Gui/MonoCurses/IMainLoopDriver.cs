namespace Terminal.Gui.MonoCurses
{
    /// <summary>
    ///     Public interface to create your own platform specific main loop driver.
    /// </summary>
    public interface IMainLoopDriver
    {
        /// <summary>
        ///     Initializes the main loop driver, gets the calling main loop for the initialization.
        /// </summary>
        /// <param name="mainLoop">Main loop.</param>
        void Setup(MainLoop mainLoop);

        /// <summary>
        ///     Wakes up the mainloop that might be waiting on input, must be thread safe.
        /// </summary>
        void Wakeup();

        /// <summary>
        ///     Must report whether there are any events pending, or even block waiting for events.
        /// </summary>
        /// <returns><c>true</c>, if there were pending events, <c>false</c> otherwise.</returns>
        /// <param name="wait">If set to <c>true</c> wait until an event is available, otherwise return immediately.</param>
        bool EventsPending(bool wait);

        void MainIteration();
    }
}