namespace Terminal.Gui {
	using System;
	using System.Runtime.InteropServices;

	static class Platform {
		static int suspendSignal;

		[DllImport("libc")]
		static extern int uname(IntPtr buf);

		[DllImport("libc")]
		static extern int killpg(int pgrp, int pid);

		static int GetSuspendSignal()
		{
			if (suspendSignal != 0)
				return suspendSignal;

			var buf = Marshal.AllocHGlobal(8192);
			if (uname(buf) != 0) {
				Marshal.FreeHGlobal(buf);
				suspendSignal = -1;
				return suspendSignal;
			}

			try {
				switch (Marshal.PtrToStringAnsi(buf)) {
				case "Darwin":
				case "DragonFly":
				case "FreeBSD":
				case "NetBSD":
				case "OpenBSD":
					suspendSignal = 18;
					break;
				case "Linux":
					// TODO: should fetch the machine name and 
					// if it is MIPS return 24
					suspendSignal = 20;
					break;
				case "Solaris":
					suspendSignal = 24;
					break;
				default:
					suspendSignal = -1;
					break;
				}

				return suspendSignal;
			} finally {
				Marshal.FreeHGlobal(buf);
			}
		}

		/// <summary>
		///     Suspends the process by sending SIGTSTP to itself
		/// </summary>
		/// <returns>The suspend.</returns>
		public static bool Suspend()
		{
			int signal = GetSuspendSignal();
			if (signal == -1)
				return false;
			killpg(0, signal);
			return true;
		}
	}
}