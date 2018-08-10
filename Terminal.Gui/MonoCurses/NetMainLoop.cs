namespace Terminal.Gui.MonoCurses
{
    using System;
    using System.Threading;

    /// <summary>
    ///     Mainloop intended to be used with the .NET System.Console API, and can
    ///     be used on Windows and Unix, it is cross platform but lacks things like
    ///     file descriptor monitoring.
    /// </summary>
    internal class NetMainLoop : IMainLoopDriver
    {
        private readonly AutoResetEvent keyReady = new AutoResetEvent(false);

        private readonly AutoResetEvent waitForProbe = new AutoResetEvent(false);

        private MainLoop mainLoop;

        public Action<ConsoleKeyInfo> WindowsKeyPressed;

        private ConsoleKeyInfo? windowsKeyResult;

        void IMainLoopDriver.Setup(MainLoop mainLoop)
        {
            this.mainLoop = mainLoop;
            var readThread = new Thread(this.WindowsKeyReader);
            readThread.Start();
        }

        void IMainLoopDriver.Wakeup()
        {
        }

        bool IMainLoopDriver.EventsPending(bool wait)
        {
            long now = DateTime.UtcNow.Ticks;

            int waitTimeout;
            if (this.mainLoop.timeouts.Count > 0)
            {
                waitTimeout = (int) ((this.mainLoop.timeouts.Keys[0] - now) / TimeSpan.TicksPerMillisecond);
                if (waitTimeout < 0)
                    return true;
            }
            else
            {
                waitTimeout = -1;
            }

            if (!wait)
                waitTimeout = 0;

            this.windowsKeyResult = null;
            this.waitForProbe.Set();
            this.keyReady.WaitOne(waitTimeout);
            return this.windowsKeyResult.HasValue;
        }

        void IMainLoopDriver.MainIteration()
        {
            if (this.windowsKeyResult.HasValue)
            {
                if (this.WindowsKeyPressed != null)
                    this.WindowsKeyPressed(this.windowsKeyResult.Value);
                this.windowsKeyResult = null;
            }
        }

        private void WindowsKeyReader()
        {
            while (true)
            {
                this.waitForProbe.WaitOne();
                this.windowsKeyResult = Console.ReadKey(true);
                this.keyReady.Set();
            }
        }
    }
}