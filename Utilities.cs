using System;
using System.Diagnostics;
using System.Text;

namespace gitedithistory
{
	public class Utilities
	{
		public static string Execute (string cmd, string args, bool capture_stdout = true, bool use_shell_execute = false, bool ignore_failure = false, bool verbose = true)
		{
			int exit_code;
			var rv = Execute (cmd, args, out exit_code, capture_stdout, use_shell_execute, verbose);

			if (exit_code != 0 && !ignore_failure)
				throw new Exception ("Program execution failed (" + exit_code + "): " + Environment.NewLine + rv.ToString ());

			return rv;
		}

		public static string Execute (string cmd, string args, out int exit_code, bool capture_stdout = true, bool use_shell_execute = false, bool verbose = true)
		{
			StringBuilder std = new StringBuilder ();

			using (System.Threading.ManualResetEvent stderr_event = new System.Threading.ManualResetEvent (false)) {
				using (System.Threading.ManualResetEvent stdout_event = new System.Threading.ManualResetEvent (false)) {

					using (Process p = new Process ()) {
						p.StartInfo.UseShellExecute = use_shell_execute && !capture_stdout;
						p.StartInfo.RedirectStandardOutput = capture_stdout;
						p.StartInfo.RedirectStandardError = capture_stdout;
						p.StartInfo.FileName = cmd;
						p.StartInfo.Arguments = args;
						p.StartInfo.CreateNoWindow = true;

						p.ErrorDataReceived += (object o, DataReceivedEventArgs ea) =>
							{
								lock (std) {
									if (ea.Data == null) {
										stderr_event.Set ();
									} else {
										std.AppendLine (ea.Data);
									}
								}
							};

						p.OutputDataReceived += (object o, DataReceivedEventArgs ea) =>
						{
							lock (std) {
								if (ea.Data == null) {
									stdout_event.Set ();
								} else {
									std.AppendLine (ea.Data);
								}
							}
						};

						if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
							switch (p.StartInfo.FileName) {
							case "git":
								p.StartInfo.FileName = @"C:\Program Files (x86)\Git\bin\git.exe";
								break;
							}
						}

						if (verbose) {
							Console.ForegroundColor = ConsoleColor.DarkBlue;
							Console.WriteLine ($"{p.StartInfo.FileName} {p.StartInfo.Arguments}");
							Console.ResetColor ();
						}

						p.Start ();

						if (capture_stdout) {
							p.BeginErrorReadLine ();
							p.BeginOutputReadLine ();
						}

						p.WaitForExit ();
						if (capture_stdout) {
							stderr_event.WaitOne ();
							stdout_event.WaitOne ();
						}

						exit_code = p.ExitCode;
						if (capture_stdout) {
							return std.ToString ();
						} else {
							return null;
						}
					}
				}
			}
		}
	}
}
