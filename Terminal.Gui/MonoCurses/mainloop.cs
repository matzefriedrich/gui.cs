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

namespace Mono.Terminal {
	using System;
	using System.Collections.Generic;
	using System.Runtime.InteropServices;
	using System.Threading;

	/// <summary>
	///     Public interface to create your own platform specific main loop driver.
	/// </summary>
	public interface IMainLoopDriver {
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

	/// <summary>
	///     Unix main loop, suitable for using on Posix systems
	/// </summary>
	/// <remarks>
	///     In addition to the general functions of the mainloop, the Unix version
	///     can watch file descriptors using the AddWatch methods.
	/// </remarks>
	public class UnixMainLoop : IMainLoopDriver {
		/// <summary>
		///     Condition on which to wake up from file descriptor activity.  These match the Linux/BSD poll definitions.
		/// </summary>
		[Flags]
		public enum Condition : short {
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

		static readonly IntPtr ignore = Marshal.AllocHGlobal(1);

		readonly Dictionary<int, Watch> descriptorWatchers = new Dictionary<int, Watch>();

		MainLoop mainLoop;

		bool poll_dirty = true;

		Pollfd[] pollmap;

		readonly int[] wakeupPipes = new int [2];

		void IMainLoopDriver.Wakeup()
		{
			write(this.wakeupPipes[1], ignore, (IntPtr) 1);
		}

		void IMainLoopDriver.Setup(MainLoop mainLoop)
		{
			this.mainLoop = mainLoop;
			pipe(this.wakeupPipes);
			this.AddWatch(this.wakeupPipes[0], Condition.PollIn, ml => {
				read(this.wakeupPipes[0], ignore, (IntPtr) 1);
				return true;
			});
		}

		bool IMainLoopDriver.EventsPending(bool wait)
		{
			long now = DateTime.UtcNow.Ticks;

			int pollTimeout, n;
			if (this.mainLoop.timeouts.Count > 0) {
				pollTimeout = (int) ((this.mainLoop.timeouts.Keys[0] - now) / TimeSpan.TicksPerMillisecond);
				if (pollTimeout < 0)
					return true;
			} else
				pollTimeout = -1;

			if (!wait)
				pollTimeout = 0;

			this.UpdatePollMap();

			n = poll(this.pollmap, (uint) this.pollmap.Length, pollTimeout);
			int ic;
			lock (this.mainLoop.idleHandlers)
				ic = this.mainLoop.idleHandlers.Count;
			return n > 0 || this.mainLoop.timeouts.Count > 0 && this.mainLoop.timeouts.Keys[0] - DateTime.UtcNow.Ticks < 0 || ic > 0;
		}

		void IMainLoopDriver.MainIteration()
		{
			if (this.pollmap != null)
				foreach (var p in this.pollmap) {
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
		static extern int poll([In] [Out] Pollfd[] ufds, uint nfds, int timeout);

		[DllImport("libc")]
		static extern int pipe([In] [Out] int[] pipes);

		[DllImport("libc")]
		static extern int read(int fd, IntPtr buf, IntPtr n);

		[DllImport("libc")]
		static extern int write(int fd, IntPtr buf, IntPtr n);

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

		void UpdatePollMap()
		{
			if (!this.poll_dirty)
				return;
			this.poll_dirty = false;

			this.pollmap = new Pollfd [this.descriptorWatchers.Count];
			var i = 0;
			foreach (int fd in this.descriptorWatchers.Keys) {
				this.pollmap[i].fd = fd;
				this.pollmap[i].events = (short) this.descriptorWatchers[fd].Condition;
				i++;
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		struct Pollfd {
			public int fd;

			public short events;

			public readonly short revents;
		}

		class Watch {
			public Func<MainLoop, bool> Callback;

			public Condition Condition;

			public int File;
		}
	}

	/// <summary>
	///     Mainloop intended to be used with the .NET System.Console API, and can
	///     be used on Windows and Unix, it is cross platform but lacks things like
	///     file descriptor monitoring.
	/// </summary>
	class NetMainLoop : IMainLoopDriver {
		readonly AutoResetEvent keyReady = new AutoResetEvent(false);

		MainLoop mainLoop;

		readonly AutoResetEvent waitForProbe = new AutoResetEvent(false);

		public Action<ConsoleKeyInfo> WindowsKeyPressed;

		ConsoleKeyInfo? windowsKeyResult;

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
			if (this.mainLoop.timeouts.Count > 0) {
				waitTimeout = (int) ((this.mainLoop.timeouts.Keys[0] - now) / TimeSpan.TicksPerMillisecond);
				if (waitTimeout < 0)
					return true;
			} else
				waitTimeout = -1;

			if (!wait)
				waitTimeout = 0;

			this.windowsKeyResult = null;
			this.waitForProbe.Set();
			this.keyReady.WaitOne(waitTimeout);
			return this.windowsKeyResult.HasValue;
		}

		void IMainLoopDriver.MainIteration()
		{
			if (this.windowsKeyResult.HasValue) {
				if (this.WindowsKeyPressed != null)
					this.WindowsKeyPressed(this.windowsKeyResult.Value);
				this.windowsKeyResult = null;
			}
		}

		void WindowsKeyReader()
		{
			while (true) {
				this.waitForProbe.WaitOne();
				this.windowsKeyResult = Console.ReadKey(true);
				this.keyReady.Set();
			}
		}
	}

	/// <summary>
	///     Simple main loop implementation that can be used to monitor
	///     file descriptor, run timers and idle handlers.
	/// </summary>
	/// <remarks>
	///     Monitoring of file descriptors is only available on Unix, there
	///     does not seem to be a way of supporting this on Windows.
	/// </remarks>
	public class MainLoop {
		internal List<Func<bool>> idleHandlers = new List<Func<bool>>();

		bool running;

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
			this.AddIdle(() => {
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
				this.idleHandlers.Add(idleHandler);
			return idleHandler;
		}

		/// <summary>
		///     Removes the specified idleHandler from processing.
		/// </summary>
		public void RemoveIdle(Func<bool> idleHandler)
		{
			lock (idleHandler)
				this.idleHandlers.Remove(idleHandler);
		}

		void AddTimeout(TimeSpan time, Timeout timeout)
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
			var timeout = new Timeout {
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

		void RunTimers()
		{
			long now = DateTime.UtcNow.Ticks;
			var copy = this.timeouts;
			this.timeouts = new SortedList<long, Timeout>();
			foreach (long k in copy.Keys) {
				var timeout = copy[k];
				if (k < now) {
					if (timeout.Callback(this))
						this.AddTimeout(timeout.Span, timeout);
				} else
					this.timeouts.Add(k, timeout);
			}
		}

		void RunIdle()
		{
			List<Func<bool>> iterate;
			lock (this.idleHandlers) {
				iterate = this.idleHandlers;
				this.idleHandlers = new List<Func<bool>>();
			}

			foreach (var idle in iterate)
				if (idle())
					lock (this.idleHandlers)
						this.idleHandlers.Add(idle);
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
				if (this.idleHandlers.Count > 0)
					this.RunIdle();
		}

		/// <summary>
		///     Runs the mainloop.
		/// </summary>
		public void Run()
		{
			bool prev = this.running;
			this.running = true;
			while (this.running) {
				this.EventsPending(true);
				this.MainIteration();
			}

			this.running = prev;
		}

		internal class Timeout {
			public Func<MainLoop, bool> Callback;

			public TimeSpan Span;
		}
	}
}