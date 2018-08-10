//
// Core.cs: The core engine for gui.cs
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
// Pending:
//   - Check for NeedDisplay on the hierarchy and repaint
//   - Layout support
//   - "Colors" type or "Attributes" type?
//   - What to surface as "BackgroundCOlor" when clearing a window, an attribute or colors?
//
// Optimziations
//   - Add rendering limitation to the exposed area

namespace Terminal.Gui
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using Terminal.Gui.Drivers;
    using Terminal.Gui.MonoCurses;
    using Terminal.Gui.Types;

    /// <summary>
    ///     The application driver for gui.cs
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         You can hook up to the Iteration event to have your method
    ///         invoked on each iteration of the mainloop.
    ///     </para>
    ///     <para>
    ///         Creates a mainloop to process input events, handle timers and
    ///         other sources of data.   It is accessible via the MainLoop property.
    ///     </para>
    ///     <para>
    ///         When invoked sets the SynchronizationContext to one that is tied
    ///         to the mainloop, allowing user code to use async/await.
    ///     </para>
    /// </remarks>
    public static class Application
    {
        /// <summary>
        ///     The current Console Driver in use.
        /// </summary>
        public static ConsoleDriver Driver;

        private static readonly Stack<Toplevel> toplevels = new Stack<Toplevel>();

        /// <summary>
        ///     If set, it forces the use of the System.Console-based driver.
        /// </summary>
        public static bool UseSystemConsole;

        private static View mouseGrabView;

        /// <summary>
        ///     Merely a debugging aid to see the raw mouse events
        /// </summary>
        public static Action<MouseEvent> RootMouseEvent;

        internal static bool DebugDrawBounds;

        /// <summary>
        ///     The Toplevel object used for the application on startup.
        /// </summary>
        /// <value>The top.</value>
        public static Toplevel Top { get; private set; }

        /// <summary>
        ///     The current toplevel object.   This is updated when Application.Run enters and leaves and points to the current
        ///     toplevel.
        /// </summary>
        /// <value>The current.</value>
        public static Toplevel Current { get; private set; }

        /// <summary>
        ///     The mainloop driver for the applicaiton
        /// </summary>
        /// <value>The main loop.</value>
        public static MainLoop MainLoop { get; private set; }

        /// <summary>
        ///     This event is raised on each iteration of the
        ///     main loop.
        /// </summary>
        /// <remarks>
        ///     See also <see cref="Timeout" />
        /// </remarks>
        public static event EventHandler Iteration;

        /// <summary>
        ///     Returns a rectangle that is centered in the screen for the provided size.
        /// </summary>
        /// <returns>The centered rect.</returns>
        /// <param name="size">Size for the rectangle.</param>
        public static Rect MakeCenteredRect(Size size)
        {
            return new Rect(new Point((Driver.Cols - size.Width) / 2, (Driver.Rows - size.Height) / 2), size);
        }

        /// <summary>
        ///     Initializes the Application
        /// </summary>
        public static void Init()
        {
            if (Top != null)
                return;

            PlatformID p = Environment.OSVersion.Platform;
            IMainLoopDriver mainLoopDriver;

            if (UseSystemConsole)
            {
                mainLoopDriver = new NetMainLoop();
                Driver = new NetDriver();
            }
            else if (p == PlatformID.Win32NT || p == PlatformID.Win32S || p == PlatformID.Win32Windows)
            {
                var windowsDriver = new WindowsDriver();
                mainLoopDriver = windowsDriver;
                Driver = windowsDriver;
            }
            else
            {
                mainLoopDriver = new UnixMainLoop();
                Driver = new CursesDriver();
            }

            Driver.Init(TerminalResized);
            MainLoop = new MainLoop(mainLoopDriver);
            SynchronizationContext.SetSynchronizationContext(new MainLoopSyncContext(MainLoop));
            Top = Toplevel.Create();
            Current = Top;
        }

        private static void ProcessKeyEvent(KeyEvent ke)
        {
            if (Current.ProcessHotKey(ke))
                return;

            if (Current.ProcessKey(ke))
                return;

            // Process the key normally
            if (Current.ProcessColdKey(ke))
                return;
        }

        private static View FindDeepestView(View start, int x, int y, out int resx, out int resy)
        {
            Rect startFrame = start.Frame;

            if (!startFrame.Contains(x, y))
            {
                resx = 0;
                resy = 0;
                return null;
            }

            if (start.Subviews != null)
            {
                int count = start.Subviews.Count;
                if (count > 0)
                {
                    int rx = x - startFrame.X;
                    int ry = y - startFrame.Y;
                    for (int i = count - 1; i >= 0; i--)
                    {
                        View v = start.Subviews[i];
                        if (v.Frame.Contains(rx, ry))
                        {
                            View deep = FindDeepestView(v, rx, ry, out resx, out resy);
                            if (deep == null)
                                return v;
                            return deep;
                        }
                    }
                }
            }

            resx = x - startFrame.X;
            resy = y - startFrame.Y;
            return start;
        }

        /// <summary>
        ///     Grabs the mouse, forcing all mouse events to be routed to the specified view until UngrabMouse is called.
        /// </summary>
        /// <returns>The grab.</returns>
        /// <param name="view">View that will receive all mouse events until UngrabMouse is invoked.</param>
        public static void GrabMouse(View view)
        {
            if (view == null)
                return;
            mouseGrabView = view;
            Driver.UncookMouse();
        }

        /// <summary>
        ///     Releases the mouse grab, so mouse events will be routed to the view on which the mouse is.
        /// </summary>
        public static void UngrabMouse()
        {
            mouseGrabView = null;
            Driver.CookMouse();
        }

        private static void ProcessMouseEvent(MouseEvent me)
        {
            RootMouseEvent?.Invoke(me);
            if (mouseGrabView != null)
            {
                Point newxy = mouseGrabView.ScreenToView(me.X, me.Y);
                var nme = new MouseEvent
                {
                    X = newxy.X,
                    Y = newxy.Y,
                    Flags = me.Flags
                };
                mouseGrabView.MouseEvent(me);
                return;
            }

            int rx, ry;
            View view = FindDeepestView(Current, me.X, me.Y, out rx, out ry);
            if (view != null)
            {
                if (!view.WantMousePositionReports && me.Flags == MouseFlags.ReportMousePosition)
                    return;

                var nme = new MouseEvent
                {
                    X = rx,
                    Y = ry,
                    Flags = me.Flags
                };
                // Should we bubbled up the event, if it is not handled?
                view.MouseEvent(nme);
            }
        }

        /// <summary>
        ///     Building block API: Prepares the provided toplevel for execution.
        /// </summary>
        /// <returns>The runstate handle that needs to be passed to the End() method upon completion.</returns>
        /// <param name="toplevel">Toplevel to prepare execution for.</param>
        /// <remarks>
        ///     This method prepares the provided toplevel for running with the focus,
        ///     it adds this to the list of toplevels, sets up the mainloop to process the
        ///     event, lays out the subviews, focuses the first element, and draws the
        ///     toplevel in the screen.   This is usually followed by executing
        ///     the <see cref="RunLoop" /> method, and then the <see cref="End(RunState)" /> method upon termination which will
        ///     undo these changes.
        /// </remarks>
        public static RunState Begin(Toplevel toplevel)
        {
            if (toplevel == null)
                throw new ArgumentNullException(nameof(toplevel));
            var rs = new RunState(toplevel);

            Init();
            toplevels.Push(toplevel);
            Current = toplevel;
            Driver.PrepareToRun(MainLoop, ProcessKeyEvent, ProcessMouseEvent);
            if (toplevel.LayoutStyle == LayoutStyle.Computed)
                toplevel.RelativeLayout(new Rect(0, 0, Driver.Cols, Driver.Rows));
            toplevel.LayoutSubviews();
            toplevel.WillPresent();
            Redraw(toplevel);
            toplevel.PositionCursor();
            Driver.Refresh();

            return rs;
        }

        /// <summary>
        ///     Building block API: completes the exection of a Toplevel that was started with Begin.
        /// </summary>
        /// <param name="runState">The runstate returned by the <see cref="Begin(Toplevel)" /> method.</param>
        public static void End(RunState runState)
        {
            if (runState == null)
                throw new ArgumentNullException(nameof(runState));

            runState.Dispose();
        }

        private static void Shutdown()
        {
            Driver.End();
        }

        private static void Redraw(View view)
        {
            view.Redraw(view.Bounds);
            Driver.Refresh();
        }

        private static void Refresh(View view)
        {
            view.Redraw(view.Bounds);
            Driver.Refresh();
        }

        /// <summary>
        ///     Triggers a refresh of the entire display.
        /// </summary>
        public static void Refresh()
        {
            Driver.UpdateScreen();
            View last = null;
            foreach (Toplevel v in toplevels.Reverse())
            {
                v.SetNeedsDisplay();
                v.Redraw(v.Bounds);
                last = v;
            }

            last?.PositionCursor();
            Driver.Refresh();
        }

        internal static void End(View view)
        {
            if (toplevels.Peek() != view)
                throw new ArgumentException("The view that you end with must be balanced");
            toplevels.Pop();
            if (toplevels.Count == 0)
            {
                Shutdown();
            }
            else
            {
                Current = toplevels.Peek();
                Refresh();
            }
        }

        /// <summary>
        ///     Building block API: Runs the main loop for the created dialog
        /// </summary>
        /// <remarks>
        ///     Use the wait parameter to control whether this is a
        ///     blocking or non-blocking call.
        /// </remarks>
        /// <param name="state">The state returned by the Begin method.</param>
        /// <param name="wait">
        ///     By default this is true which will execute the runloop waiting for events, if you pass false, you
        ///     can use this method to run a single iteration of the events.
        /// </param>
        public static void RunLoop(RunState state, bool wait = true)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (state.Toplevel == null)
                throw new ObjectDisposedException("state");

            for (state.Toplevel.Running = true; state.Toplevel.Running;)
            {
                if (MainLoop.EventsPending(wait))
                {
                    MainLoop.MainIteration();
                    if (Iteration != null)
                        Iteration(null, EventArgs.Empty);
                }
                else if (wait == false)
                {
                    return;
                }

                if (!state.Toplevel.NeedDisplay.IsEmpty || state.Toplevel.childNeedsDisplay)
                {
                    state.Toplevel.Redraw(state.Toplevel.Bounds);
                    if (DebugDrawBounds)
                        DrawBounds(state.Toplevel);
                    state.Toplevel.PositionCursor();
                    Driver.Refresh();
                }
                else
                {
                    Driver.UpdateCursor();
                }
            }
        }

        // Need to look into why this does not work properly.
        private static void DrawBounds(View v)
        {
            v.DrawFrame(v.Frame, 0, false);
            if (v.Subviews != null && v.Subviews.Count > 0)
                foreach (View sub in v.Subviews)
                    DrawBounds(sub);
        }

        /// <summary>
        ///     Runs the application with the built-in toplevel view
        /// </summary>
        public static void Run()
        {
            Run(Top);
        }

        /// <summary>
        ///     Runs the main loop on the given container.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This method is used to start processing events
        ///         for the main application, but it is also used to
        ///         run modal dialog boxes.
        ///     </para>
        ///     <para>
        ///         To make a toplevel stop execution, set the "Running"
        ///         property to false.
        ///     </para>
        ///     <para>
        ///         This is equivalent to calling Begin on the toplevel view, followed by RunLoop with the
        ///         returned value, and then calling end on the return value.
        ///     </para>
        ///     <para>
        ///         Alternatively, if your program needs to control the main loop and needs to
        ///         process events manually, you can invoke Begin to set things up manually and then
        ///         repeatedly call RunLoop with the wait parameter set to false.   By doing this
        ///         the RunLoop method will only process any pending events, timers, idle handlers and
        ///         then return control immediately.
        ///     </para>
        /// </remarks>
        public static void Run(Toplevel view)
        {
            RunState runToken = Begin(view);
            RunLoop(runToken);
            End(runToken);
        }

        /// <summary>
        ///     Stops running the most recent toplevel
        /// </summary>
        public static void RequestStop()
        {
            Current.Running = false;
        }

        private static void TerminalResized()
        {
            var full = new Rect(0, 0, Driver.Cols, Driver.Rows);
            Driver.Clip = full;
            foreach (Toplevel t in toplevels)
            {
                t.RelativeLayout(full);
                t.LayoutSubviews();
            }

            Refresh();
        }

        //
        // provides the sync context set while executing code in gui.cs, to let
        // users use async/await on their code
        //
        private class MainLoopSyncContext : SynchronizationContext
        {
            private readonly MainLoop mainLoop;

            public MainLoopSyncContext(MainLoop mainLoop)
            {
                this.mainLoop = mainLoop;
            }

            public override SynchronizationContext CreateCopy()
            {
                return new MainLoopSyncContext(MainLoop);
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                this.mainLoop.AddIdle(() =>
                {
                    d(state);
                    return false;
                });
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                this.mainLoop.Invoke(() => { d(state); });
            }
        }

        /// <summary>
        ///     Captures the execution state for the provided TopLevel view.
        /// </summary>
        public class RunState : IDisposable
        {
            internal Toplevel Toplevel;

            internal RunState(Toplevel view)
            {
                this.Toplevel = view;
            }

            /// <summary>
            ///     Releases alTop = l resource used by the <see cref="T:Terminal.Gui.Application.RunState" /> object.
            /// </summary>
            /// <remarks>
            ///     Call <see cref="Dispose()" /> when you are finished using the <see cref="T:Terminal.Gui.Application.RunState" />.
            ///     The
            ///     <see cref="Dispose()" /> method leaves the <see cref="T:Terminal.Gui.Application.RunState" /> in an unusable state.
            ///     After
            ///     calling <see cref="Dispose()" />, you must release all references to the
            ///     <see cref="T:Terminal.Gui.Application.RunState" /> so the garbage collector can reclaim the memory that the
            ///     <see cref="T:Terminal.Gui.Application.RunState" /> was occupying.
            /// </remarks>
            public void Dispose()
            {
                this.Dispose(true);
                GC.SuppressFinalize(this);
            }

            /// <summary>
            ///     Dispose the specified disposing.
            /// </summary>
            /// <returns>The dispose.</returns>
            /// <param name="disposing">If set to <c>true</c> disposing.</param>
            protected virtual void Dispose(bool disposing)
            {
                if (this.Toplevel != null)
                {
                    End(this.Toplevel);
                    this.Toplevel = null;
                }
            }
        }
    }
}