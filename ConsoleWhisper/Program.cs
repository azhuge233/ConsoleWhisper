using CommandLine;
using CommandLine.Text;
using ConsoleWhisper.Model;
using ConsoleWhisper.Module;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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
			} catch (IOException ex) {
				if (!string.IsNullOrEmpty(ex.Message)) Output.Error($"IO Error: {ex.Message}");
				return;
			} catch (HttpRequestException ex) {
				if (!string.IsNullOrEmpty(ex.Message)) Output.Error($"Http Request Error: {ex.Message}");
				return;
			} catch (Exception ex) {
				if (!string.IsNullOrEmpty(ex.Message)) Output.Error($"Unknown Error: {ex.Message}");
				return;
			}
		}

		static async Task Run(Argument arg) {
			try {
				arg.Validate();

				arg.ModelType = FileHelper.GetModelName(arg.ModelType);
				arg.Files = FileHelper.ExpandFilePaths(arg.Files);

				if (!FileHelper.ModelExists(arg.ModelType)) {
					Output.Warn($"{arg.ModelType} is not found in Models directory, start downloading.");
					await WhisperHelper.DownloadModel(arg.ModelType);
				}

				int cnt = 1;
				foreach (var file in arg.Files) {
					var mediaFilename = Path.GetFileName(file);
					var wavFilename = await AudioHelper.Extract(file);
					Output.Info($"Start transcribing file #{cnt++}: {mediaFilename}");
					await WhisperHelper.Transcribe(arg.ModelType, wavFilename, mediaFilename, arg.OutputDir);
					FileHelper.DelFile(wavFilename);
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