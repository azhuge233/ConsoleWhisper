using System;

namespace ConsoleWhisper.Module {
	internal static class Output {
		internal static void ResetColor() {
			Console.ResetColor();
		}
		internal static void Error(string msg) {
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(msg);
			ResetColor();
		}
		internal static void Warn(string msg) {
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(msg);
			ResetColor();
		}
		internal static void WarnR(string msg) {
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.Write(msg);
			ResetColor();
		}
		internal static void Success(string msg) {
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine(msg);
			ResetColor();
		}
		internal static void Info(string msg) {
			Console.WriteLine(msg);
			ResetColor();
		}
		internal static void InfoR(string msg) {
			Console.Write(msg);
			ResetColor();
		}

		internal static void Help(string msg, bool isHelp, bool isVersion) {
			// Help / version requests print the whole text as is. For parse errors
			// CommandLineParser puts the error block on top of the usage text: print that
			// block in red and the usage text normally - no line count arithmetic needed.
			if (isHelp || isVersion) {
				Console.WriteLine(msg);
				return;
			}

			var errorBlock = true;
			foreach (var line in msg.Split(Environment.NewLine)) {
				if (errorBlock && line.Length == 0)
					errorBlock = false;

				if (errorBlock)
					Error(line);
				else
					Console.WriteLine(line);
			}
		}
	}
}
