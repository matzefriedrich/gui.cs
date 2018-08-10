//
// mainloop.cs: Simple managed mainloop implementation.
//
// Authors:
//   Miguel de Icaza (miguel.de.icaza@gmail.com)
//
// Copyright (C) 2011 Novell (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

namespace Mono.Terminal
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     Simple main loop implementation that can be used to monitor
    ///     file descriptor, run timers and idle handlers.
    /// </summary>
    /// <remarks>
    ///     Monitoring of file descriptors is only available on Unix, there
    ///     does not seem to be a way of supporting this on Windows.
    /// </remarks>
    public class MainLoop
    {
        internal List<Func<bool>> idleHandlers = new List<Func<bool>>();

        private bool running;

        internal SortedList<long, Timeout> timeouts = new SortedList<long, Timeout>();

        /// <summary>
        ///     Creates a new Mainloop, to run it you must provide a driver, and choose
        ///     one of the implementations UnixMainLoop, NetMainLoop or WindowsMainLoop.
        /// </summary>
        public MainLoop(IMainLoopDriver driver)
        {
            this.Driver = driver;
            driver.Setup(this);
        }

        /// <summary>
        ///     The current IMainLoopDriver in use.
        /// </summary>
        /// <value>The driver.</value>
        public IMainLoopDriver Driver { get; }

        /// <summary>
        ///     Runs @action on the thread that is processing events
        /// </summary>
        public void Invoke(Action action)
        {
            this.AddIdle(() =>
            {
                action();
                return false;
            });
            this.Driver.Wakeup();
        }

        /// <summary>
        ///     Executes the specified @idleHandler on the idle loop.  The return value is a token to remove it.
        /// </summary>
        public Func<bool> AddIdle(Func<bool> idleHandler)
        {
            lock (this.idleHandlers)
            {
                this.idleHandlers.Add(idleHandler);
            }

            return idleHandler;
        }

        /// <summary>
        ///     Removes the specified idleHandler from processing.
        /// </summary>
        public void RemoveIdle(Func<bool> idleHandler)
        {
            lock (idleHandler)
            {
                this.idleHandlers.Remove(idleHandler);
            }
        }

        private void AddTimeout(TimeSpan time, Timeout timeout)
        {
            this.timeouts.Add((DateTime.UtcNow + time).Ticks, timeout);
        }

        /// <summary>
        ///     Adds a timeout to the mainloop.
        /// </summary>
        /// <remarks>
        ///     When time time specified passes, the callback will be invoked.
        ///     If the callback returns true, the timeout will be reset, repeating
        ///     the invocation. If it returns false, the timeout will stop.
        ///     The returned value is a token that can be used to stop the timeout
        ///     by calling RemoveTimeout.
        /// </remarks>
        public object AddTimeout(TimeSpan time, Func<MainLoop, bool> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            var timeout = new Timeout
            {
                Span = time,
                Callback = callback
            };
            this.AddTimeout(time, timeout);
            return timeout;
        }

        /// <summary>
        ///     Removes a previously scheduled timeout
        /// </summary>
        /// <remarks>
        ///     The token parameter is the value returned by AddTimeout.
        /// </remarks>
        public void RemoveTimeout(object token)
        {
            int idx = this.timeouts.IndexOfValue(token as Timeout);
            if (idx == -1)
                return;
            this.timeouts.RemoveAt(idx);
        }

        private void RunTimers()
        {
            long now = DateTime.UtcNow.Ticks;
            SortedList<long, Timeout> copy = this.timeouts;
            this.timeouts = new SortedList<long, Timeout>();
            foreach (long k in copy.Keys)
            {
                Timeout timeout = copy[k];
                if (k < now)
                {
                    if (timeout.Callback(this))
                        this.AddTimeout(timeout.Span, timeout);
                }
                else
                {
                    this.timeouts.Add(k, timeout);
                }
            }
        }

        private void RunIdle()
        {
            List<Func<bool>> iterate;
            lock (this.idleHandlers)
            {
                iterate = this.idleHandlers;
                this.idleHandlers = new List<Func<bool>>();
            }

            foreach (Func<bool> idle in iterate)
                if (idle())
                    lock (this.idleHandlers)
                    {
                        this.idleHandlers.Add(idle);
                    }
        }

        /// <summary>
        ///     Stops the mainloop.
        /// </summary>
        public void Stop()
        {
            this.running = false;
            this.Driver.Wakeup();
        }

        /// <summary>
        ///     Determines whether there are pending events to be processed.
        /// </summary>
        /// <remarks>
        ///     You can use this method if you want to probe if events are pending.
        ///     Typically used if you need to flush the input queue while still
        ///     running some of your own code in your main thread.
        /// </remarks>
        public bool EventsPending(bool wait = false)
        {
            return this.Driver.EventsPending(wait);
        }

        /// <summary>
        ///     Runs one iteration of timers and file watches
        /// </summary>
        /// <remarks>
        ///     You use this to process all pending events (timers, idle handlers and file watches).
        ///     You can use it like this:
        ///     while (main.EvensPending ()) MainIteration ();
        /// </remarks>
        public void MainIteration()
        {
            if (this.timeouts.Count > 0)
                this.RunTimers();

            this.Driver.MainIteration();

            lock (this.idleHandlers)
            {
                if (this.idleHandlers.Count > 0)
                    this.RunIdle();
            }
        }

        /// <summary>
        ///     Runs the mainloop.
        /// </summary>
        public void Run()
        {
            bool prev = this.running;
            this.running = true;
            while (this.running)
            {
                this.EventsPending(true);
                this.MainIteration();
            }

            this.running = prev;
        }

        internal class Timeout
        {
            public Func<MainLoop, bool> Callback;

            public TimeSpan Span;
        }
    }
}