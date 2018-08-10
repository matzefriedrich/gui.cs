namespace Mono.Terminal
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    /// <summary>
    ///     Unix main loop, suitable for using on Posix systems
    /// </summary>
    /// <remarks>
    ///     In addition to the general functions of the mainloop, the Unix version
    ///     can watch file descriptors using the AddWatch methods.
    /// </remarks>
    public class UnixMainLoop : IMainLoopDriver
    {
        /// <summary>
        ///     Condition on which to wake up from file descriptor activity.  These match the Linux/BSD poll definitions.
        /// </summary>
        [Flags]
        public enum Condition : short
        {
            /// <summary>
            ///     There is data to read
            /// </summary>
            PollIn = 1,

            /// <summary>
            ///     Writing to the specified descriptor will not block
            /// </summary>
            PollOut = 4,

            /// <summary>
            ///     There is urgent data to read
            /// </summary>
            PollPri = 2,

            /// <summary>
            ///     Error condition on output
            /// </summary>
            PollErr = 8,

            /// <summary>
            ///     Hang-up on output
            /// </summary>
            PollHup = 16,

            /// <summary>
            ///     File descriptor is not open.
            /// </summary>
            PollNval = 32
        }

        private static readonly IntPtr ignore = Marshal.AllocHGlobal(1);

        private readonly Dictionary<int, Watch> descriptorWatchers = new Dictionary<int, Watch>();

        private readonly int[] wakeupPipes = new int [2];

        private MainLoop mainLoop;

        private bool poll_dirty = true;

        private Pollfd[] pollmap;

        void IMainLoopDriver.Wakeup()
        {
            write(this.wakeupPipes[1], ignore, (IntPtr) 1);
        }

        void IMainLoopDriver.Setup(MainLoop mainLoop)
        {
            this.mainLoop = mainLoop;
            pipe(this.wakeupPipes);
            this.AddWatch(this.wakeupPipes[0], Condition.PollIn, ml =>
            {
                read(this.wakeupPipes[0], ignore, (IntPtr) 1);
                return true;
            });
        }

        bool IMainLoopDriver.EventsPending(bool wait)
        {
            long now = DateTime.UtcNow.Ticks;

            int pollTimeout, n;
            if (this.mainLoop.timeouts.Count > 0)
            {
                pollTimeout = (int) ((this.mainLoop.timeouts.Keys[0] - now) / TimeSpan.TicksPerMillisecond);
                if (pollTimeout < 0)
                    return true;
            }
            else
            {
                pollTimeout = -1;
            }

            if (!wait)
                pollTimeout = 0;

            this.UpdatePollMap();

            n = poll(this.pollmap, (uint) this.pollmap.Length, pollTimeout);
            int ic;
            lock (this.mainLoop.idleHandlers)
            {
                ic = this.mainLoop.idleHandlers.Count;
            }

            return n > 0 || this.mainLoop.timeouts.Count > 0 && this.mainLoop.timeouts.Keys[0] - DateTime.UtcNow.Ticks < 0 || ic > 0;
        }

        void IMainLoopDriver.MainIteration()
        {
            if (this.pollmap != null)
                foreach (Pollfd p in this.pollmap)
                {
                    Watch watch;

                    if (p.revents == 0)
                        continue;

                    if (!this.descriptorWatchers.TryGetValue(p.fd, out watch))
                        continue;
                    if (!watch.Callback(this.mainLoop))
                        this.descriptorWatchers.Remove(p.fd);
                }
        }

        [DllImport("libc")]
        private static extern int poll([In] [Out] Pollfd[] ufds, uint nfds, int timeout);

        [DllImport("libc")]
        private static extern int pipe([In] [Out] int[] pipes);

        [DllImport("libc")]
        private static extern int read(int fd, IntPtr buf, IntPtr n);

        [DllImport("libc")]
        private static extern int write(int fd, IntPtr buf, IntPtr n);

        /// <summary>
        ///     Removes an active watch from the mainloop.
        /// </summary>
        /// <remarks>
        ///     The token parameter is the value returned from AddWatch
        /// </remarks>
        public void RemoveWatch(object token)
        {
            var watch = token as Watch;
            if (watch == null)
                return;
            this.descriptorWatchers.Remove(watch.File);
        }

        /// <summary>
        ///     Watches a file descriptor for activity.
        /// </summary>
        /// <remarks>
        ///     When the condition is met, the provided callback
        ///     is invoked.  If the callback returns false, the
        ///     watch is automatically removed.
        ///     The return value is a token that represents this watch, you can
        ///     use this token to remove the watch by calling RemoveWatch.
        /// </remarks>
        public object AddWatch(int fileDescriptor, Condition condition, Func<MainLoop, bool> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            var watch = new Watch {Condition = condition, Callback = callback, File = fileDescriptor};
            this.descriptorWatchers[fileDescriptor] = watch;
            this.poll_dirty = true;
            return watch;
        }

        private void UpdatePollMap()
        {
            if (!this.poll_dirty)
                return;
            this.poll_dirty = false;

            this.pollmap = new Pollfd [this.descriptorWatchers.Count];
            var i = 0;
            foreach (int fd in this.descriptorWatchers.Keys)
            {
                this.pollmap[i].fd = fd;
                this.pollmap[i].events = (short) this.descriptorWatchers[fd].Condition;
                i++;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Pollfd
        {
            public int fd;

            public short events;

            public readonly short revents;
        }

        private class Watch
        {
            public Func<MainLoop, bool> Callback;

            public Condition Condition;

            public int File;
        }
    }
}