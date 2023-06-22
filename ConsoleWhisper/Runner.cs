using CommandLine.Text;
using CommandLine;
using ConsoleWhisper.Model;
using ConsoleWhisper.Module;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System;
using Xabe.FFmpeg.Downloader;
using Xabe.FFmpeg;

namespace ConsoleWhisper {
	internal static class Runner {
		internal static async Task Run(Argument arg) {
			try {
				arg.Validate();

				arg.ModelType = FileHelper.GetModelName(arg.ModelType);
				arg.Files = FileHelper.ExpandFilePaths(arg.Files);

				FFmpeg.SetExecutablesPath(FileHelper.AppDirectory);

				if (!FileHelper.FFmpegExists()) {
					Output.Warn($"FFmpeg not found, start downloading.");
					await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official);
				}

				if (!FileHelper.ModelExists(arg.ModelType)) {
					Output.Warn($"{arg.ModelType} is not found in Models directory, start downloading.");
					await WhisperHelper.DownloadModel(arg.ModelType);
				}

				int cnt = 1;
				foreach (var file in arg.Files) {
					var mediaFilename = Path.GetFileName(file);
					var wavFilename = await AudioHelper.Extract(arg.OutputDir, file, arg.OnlyExtract);

					if (!arg.OnlyExtract) {
						Output.Info($"Start transcribing file #{cnt++}: {mediaFilename}");
						await WhisperHelper.Transcribe(arg.ModelType, wavFilename, mediaFilename, arg.OutputDir, arg.Language);
						FileHelper.DelFile(wavFilename);
					}
				}


			} catch (Exception) {
				throw;
			}
		}

		internal static void Error<T>(ParserResult<T> result, List<Error> errs) {
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
