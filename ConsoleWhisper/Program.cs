using CommandLine;
using CommandLine.Text;
using ConsoleWhisper.Model;
using ConsoleWhisper.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConsoleWhisper {
	internal class Program {
		static async Task Main(string[] args) {
			var parseResult = new Parser(options => options.HelpWriter = null)
								.ParseArguments<Argument>(args);

			try {
				await parseResult.WithParsedAsync(Run);
				parseResult.WithNotParsed(errs => Error(parseResult, errs.ToList()));
			} catch (ArgumentException ex) {
				if (!string.IsNullOrEmpty(ex.Message)) Output.Error($"Argument Error: {ex.Message}");
				return;
			}
		}

		static async Task Run(Argument arg) {
			try {
				arg.Validate();

				foreach (var file in arg.Files) {
					await WaveAudioExtractor.Extract(file);
				}


			} catch (Exception) {
				throw;
			}
		}

		static void Error<T>(ParserResult<T> result, List<Error> errs) {
			var helpText = HelpText.AutoBuild(result, options => {
				options.AdditionalNewLineAfterOption = false;
				options.MaximumDisplayWidth = Console.WindowWidth;
				options.AutoHelp = true;
				options.AutoVersion = true;
				return HelpText.DefaultParsingErrorsHandler(result, options);
			}, e => e);

			Output.Help(helpText, errs.IsHelp(), errs.IsVersion());
		}
	}
}