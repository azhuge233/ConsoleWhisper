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

		internal static void Help(string msg, bool versionOnly = false) {
			// The auto built help text starts with a header (tool name + version, copyright).
			// That header is metadata, not an error - it stays in the standard colours even when
			// it precedes the error block of a parse failure; only the error block is red.
			// A version request shows that header and nothing else.
			var inHeader = true;
			var inErrorSection = false;

			foreach (var line in msg.Split(Environment.NewLine)) {
				if (inHeader) {
					if (line.Length == 0) {
						inHeader = false;

						if (versionOnly)
							return;

						Console.WriteLine(line);
						continue;
					}

					WriteHeaderLine(line);
					continue;
				}

				if (line.StartsWith("ERROR", StringComparison.Ordinal))
					inErrorSection = true;
				else if (inErrorSection && line.Length == 0)
					inErrorSection = false;

				if (inErrorSection)
					Error(line);
				else
					Console.WriteLine(line);
			}
		}

		/// <summary>
		/// Reports download progress on a single overwriting line (a terminal) or at most one line
		/// per step when the output is redirected. Pass a total of 0 when the size is unknown.
		/// </summary>
		internal static void ReportDownload(string label, long downloadedBytes, long totalBytes) {
			if (totalBytes > 0) {
				var percent = (int)(downloadedBytes * 100 / totalBytes);

				if (percent < 100 && percent < lastReportedPercent + ProgressStepPercent)
					return;

				lastReportedPercent = percent;
				Progress($"Downloading {label}: {percent}% ({FormatSize(downloadedBytes)} of {FormatSize(totalBytes)})");
				return;
			}

			if (downloadedBytes - lastReportedBytes < ProgressStepBytes)
				return;

			lastReportedBytes = downloadedBytes;
			Progress($"Downloading {label}: {FormatSize(downloadedBytes)}");
		}

		internal static void CompleteDownload(string label) {
			lastReportedPercent = -1;
			lastReportedBytes = 0;

			Progress($"Downloaded {label}.");
			EndProgress();
		}

		internal static string FormatSize(long bytes) {
			if (bytes >= 1024L * 1024 * 1024)
				return $"{bytes / (1024.0 * 1024 * 1024):0.##} GB";

			return $"{bytes / (1024.0 * 1024):0.#} MB";
		}

		private static void Progress(string text) {
			if (Console.IsOutputRedirected) {
				Console.WriteLine(text);
				return;
			}

			var padding = Math.Max(0, lastProgressLength - text.Length);
			Console.Write($"\r{text}{new string(' ', padding)}");
			lastProgressLength = text.Length;
		}

		private static void EndProgress() {
			if (!Console.IsOutputRedirected && lastProgressLength > 0)
				Console.WriteLine();

			lastProgressLength = 0;
		}

		private static void WriteHeaderLine(string line) {
			if (line.StartsWith("Copyright", StringComparison.Ordinal)) {
				Console.ForegroundColor = ConsoleColor.DarkGray;
				Console.WriteLine(line);
				ResetColor();
				return;
			}

			Console.WriteLine(line);
		}

		private const int ProgressStepPercent = 5;
		private const long ProgressStepBytes = 10L * 1024 * 1024;

		private static int lastReportedPercent = -1;
		private static long lastReportedBytes;
		private static int lastProgressLength;
	}
}
