using System;
using System.IO;
using System.Linq;
using System.Text;

namespace gitedithistory
{
	class MainClass
	{
		public static int Main (string [] args)
		{
			if (args.Length == 0) {
				Console.WriteLine ("The start of the commit range must be specified.");
				return 1;
			}
			if (args.Length > 1) {
				Console.WriteLine ("Only the start of the commit range can be specified.");
				return 1;
			}
			return GitEditHistory (args [0]);
		}

		static int GitEditHistory (string start_spec)
		{
			int exit_code;
			var is_in_branch = Utilities.Execute ("git", $"branch --contains {start_spec}", out exit_code);
			if (exit_code != 0) {
				Console.Error.WriteLine ($"Failed to check the current branch:");
				Console.Error.WriteLine (is_in_branch);
				return 1;
			}
			if (string.IsNullOrEmpty (is_in_branch)) {
				Console.Error.WriteLine ($"The spec {start_spec} is not in the current branch's history.");
				return 1;
			}
			var log = Utilities.Execute ("git", $"log {start_spec}..HEAD --pretty=%H", out exit_code);
			if (exit_code != 0) {
				Console.Error.WriteLine ($"Failed to list the commits for {start_spec}..HEAD:");
				Console.Error.WriteLine (log);
				return 1;
				
			}
			var commits = log.Split (new char [] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
			var sb = new StringBuilder ();

			var messages = new string [commits.Length];
			var diffs = new string [commits.Length];
			var edited_messages = new string [commits.Length];
			var message_diff = new string [commits.Length];

			var index = 0;
			while (true) {
				if (index < 0)
					index = commits.Length - 1;
				else if (index >= commits.Length)
					index = 0;
				
				Console.Clear ();
				using (var stdout = Console.OpenStandardOutput ()) {
					stdout.WriteByte (0x1b);
					stdout.WriteByte (0x5b);
					stdout.WriteByte (0x33);
					stdout.WriteByte (0x4a);
				}
				Console.WriteLine ($"Reviewing commit #{index + 1}/{commits.Length} commits ({commits [index]}). There are {edited_messages.Count ((v) => v != null)} edited commits.");
				Console.WriteLine ();

				if (messages [index] == null) {
					messages [index] = Utilities.Execute ("git", $"log -1 --pretty=%B {commits [index]}", verbose: false);
					diffs [index] = Utilities.Execute ("git", $"diff --color=always {commits [index]}^..{commits [index]}", verbose: false);
				}

				if (edited_messages [index] == null) {
					Console.Write (messages [index]);
				} else {
					if (message_diff [index] == null) {
						var tmpA = Path.GetTempFileName ();
						var tmpB = Path.GetTempFileName ();

						File.WriteAllText (tmpA, messages [index]);
						File.WriteAllText (tmpB, edited_messages [index]);
						message_diff [index] = Utilities.Execute ("git", $"diff --color=always {tmpA} {tmpB}", ignore_failure: true, verbose: false);

						File.Delete (tmpA);
						File.Delete (tmpB);
					}

					Console.ForegroundColor = ConsoleColor.White;
					Console.WriteLine ("This commit message has been modified.");
					Console.WriteLine ();
					Console.ForegroundColor = ConsoleColor.White;
					Console.WriteLine (new string ('-', Console.BufferWidth));
					Console.ForegroundColor = ConsoleColor.DarkGreen;
					Console.WriteLine (edited_messages [index]);
					Console.ForegroundColor = ConsoleColor.White;
					Console.WriteLine (new string ('-', Console.BufferWidth));
					Console.ResetColor ();
					Console.Write (message_diff [index]);
				}
				Console.ForegroundColor = ConsoleColor.White;
				Console.WriteLine (new string ('-', Console.BufferWidth));
				Console.ResetColor ();
				Console.WriteLine ();
				Console.WriteLine (diffs [index]);

				sb.Clear ();
				var loop = true;
				while (loop) {
					var key = Console.ReadKey (true);
					switch (key.Key) {
					case ConsoleKey.UpArrow:
						sb.Append ("u");
						loop = false;
						break;
					case ConsoleKey.DownArrow:
						sb.Append ("d");
						loop = false;
						break;
					case ConsoleKey.Enter:
						break;
					default:
						if (key.KeyChar != 0) {
							sb.Append (key.KeyChar);
							loop = false;
						}
						break;
					}
				}
				var line = sb.ToString ();
				switch (line) {
				case "s": // save
					if (edited_messages [index] != null) {
						Console.ForegroundColor = ConsoleColor.White;
						Console.WriteLine ("Saving edited commit message...");
						Console.ResetColor ();
						Utilities.Execute ("git", "backup", capture_stdout: false);

						// reset to the hash in question
						Utilities.Execute ("git", $"reset --hard {commits [index]}", capture_stdout: false);
						// Change the commit message
						var tmpFile = Path.GetTempFileName ();
						File.WriteAllText (tmpFile, edited_messages [index]);
						Utilities.Execute ("git", $"commit --amend -F {tmpFile}", capture_stdout: false);
						File.Delete (tmpFile);

						// Replay the rest of the commits
						for (int i = index - 1; i >= 0; i--)
							Utilities.Execute ("git", $"cherry-pick {commits [i]}", capture_stdout: false);

						messages [index] = null;
						edited_messages [index] = null;
						message_diff [index] = null;

						log = Utilities.Execute ("git", $"log {start_spec}..HEAD --pretty=%H");
						commits = log.Split (new char [] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

						Console.ForegroundColor = ConsoleColor.White;
						Console.WriteLine ("The commit message was successfully edited. Press a key to continue.");
						Console.ResetColor ();
					} else {
						Console.ForegroundColor = ConsoleColor.White;
						Console.WriteLine ("No edited messages for this commit. Press a key to continue.");
						Console.ResetColor ();
					}
					Console.ReadKey (true);
					break;
				case "u": // up
					index++;
					break;
				case "d": // down
					index--;
					break;
				case "r": // refresh
					// Nothing to do
					break;
				case "c": // clear
					if (edited_messages [index] != null) {
						Console.ForegroundColor = ConsoleColor.White;
						Console.WriteLine ("Clear the edited message for this commit? Press 'y' to confirm.");
						Console.ResetColor ();
						if (Console.ReadKey ().Key == ConsoleKey.Y) {
							edited_messages [index] = null;
							message_diff [index] = null;
							Console.ForegroundColor = ConsoleColor.White;
							Console.WriteLine ("The edited message was cleared. Press a key to continue.");
							Console.ResetColor ();
						} else {
							Console.ForegroundColor = ConsoleColor.White;
							Console.WriteLine ("The edited message was not cleared. Press a key to continue.");
							Console.ResetColor ();
						}
						Console.ReadKey (true);
					} else {
						Console.ForegroundColor = ConsoleColor.White;
						Console.WriteLine ("No edited messages for this commit. Press a key to continue.");
						Console.ResetColor ();
						Console.ReadKey ();
					}
					break;
				case "e": // edit
					Console.ForegroundColor = ConsoleColor.White;
					Console.WriteLine ("Editing commit message...");
					Console.ResetColor ();
					var tmpfile = Path.GetTempFileName ();

					// Remove multiple newlines at the end.
					var msg = edited_messages [index] ?? messages [index];
					while (msg.Length > 2 && msg [msg.Length - 1] == '\n' && msg [msg.Length - 2] == '\n')
						msg = msg.Substring (0, msg.Length - 1);

					File.WriteAllText (tmpfile, msg);
					Utilities.Execute ("/Applications/Sublime Text.app/Contents/SharedSupport/bin/subl", $"-n -w {tmpfile}");
					var updated = File.ReadAllText (tmpfile);

					// Remove multiple newlines at the end.
					while (updated.Length > 2 && updated [updated.Length - 1] == '\n' && updated [updated.Length - 2] == '\n')
						updated = updated.Substring (0, updated.Length - 1);
					
					if (updated != messages [index]) {
						edited_messages [index] = updated;
					} else {
						edited_messages [index] = null;
					}
					message_diff [index] = null;
					File.Delete (tmpfile);
					break;
				case "q": // quit
					if (edited_messages.Count ((v) => v != null) > 0) {
						Console.ForegroundColor = ConsoleColor.White;
						Console.Error.Write ("There are unsaved edits. Are you sure you want to quit? Press 'y' to exit.");
						Console.ResetColor ();
						if (Console.ReadKey ().Key != ConsoleKey.Y)
							break;
					}
					return 0;
				default:
					if (line.Length > 0) {
						Console.ForegroundColor = ConsoleColor.White;
						Console.Error.WriteLine ("Unknown command: {0}. Press a key to continue.", line);
						Console.ResetColor ();
						Console.ReadKey (true);
					}
					break;
				}
			}
		}
	}
}
