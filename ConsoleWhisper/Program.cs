using CommandLine;
using ConsoleWhisper.Model;
using ConsoleWhisper.Module;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace ConsoleWhisper {
	internal class Program {
		static async Task<int> Main(string[] args) {
			var parseResult = new Parser(options => options.HelpWriter = null)
								.ParseArguments<Argument>(args);

			if (parseResult is not Parsed<Argument> parsed)
				return Runner.Error(parseResult, [.. parseResult.Errors]);

			try {
				return await Runner.Run(parsed.Value);
			} catch (NoMatchException ex) {
				ReportException(ex, parsed.Value.Verbose);
				return ExitCode.NoMatch;
			} catch (Exception ex) {
				ReportException(ex, parsed.Value.Verbose);
				return ExitCode.Fatal;
			}
		}

		private static void ReportException(Exception exception, bool verbose) {
			if (verbose) {
				Output.Error(exception.ToString());
				return;
			}

			var prefix = exception switch {
				NoMatchException => "Error",
				NotSupportedException => "Error",
				ArgumentException => "Argument Error",
				IOException => "IO Error",
				HttpRequestException => "Http Request Error",
				_ => "Unknown Error"
			};

			Output.Error(string.IsNullOrEmpty(exception.Message) ? $"{prefix}." : $"{prefix}: {exception.Message}");
		}
	}
}