using CommandLine.Text;
using CommandLine;
using ConsoleWhisper.Model;
using ConsoleWhisper.Module;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace ConsoleWhisper {
	internal static class Runner {
		internal static async Task Run(Argument arg) {
			try {
				arg.Validate();

				arg.ModelType = FileHelper.GetModelName(arg.ModelType);
				var mediaFileList = FileHelper.ExpandFilePaths(arg.Files).ToList();

				await FileHelper.DownloadFFmpegandModel(arg);

				if (arg.Multithread) {
					await DoMultithread(mediaFileList, arg);
				} else {
					await DoSinglethread(mediaFileList, arg);
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

		private static async Task DoMultithread(List<string> mediaFileList, Argument arg) {
			try {
				Output.WarnR($"Use multithreading will disable user input.\n" +
						$"If media file has multiple soundtracks, program will extract the first one, you will NOT be able to choose.\n" +
						$"If you are transcribing, make sure to have enough disk space to store the temp .wav file.\n" +
						$"Proceed? yes(y)/No(N): ");

				var confirm = Console.ReadLine().TrimEnd(Environment.NewLine.ToCharArray());
				if (confirm.ToLower() != "y" && confirm.ToLower() != "yes") {
					Output.Error($"Aborted.");
					return;
				}

				var tasks = Enumerable.Range(0, mediaFileList.Count)
							.Select(i => Task.Run(() => DoExtractMultithread(mediaFileList[i], arg)));

				var mediaWavMap = await Task.WhenAll(tasks);

				if (!arg.OnlyExtract) {
					await DoTranscribeMultithread(mediaWavMap, arg);
				}
			} catch (Exception) {
				throw;
			}
		}

		private static async Task DoSinglethread(List<string> mediaFileList, Argument arg) {
			try {
				int cnt = 1;
				foreach (var mediaFilename in mediaFileList) {
					var wavFilename = await DoExtract(mediaFilename, arg);
					if (!arg.OnlyExtract) {
						await DoTranscribe(wavFilename, mediaFilename, cnt++, arg);
					}
				}
			} catch (Exception) {
				throw;
			}
		}

		private static async Task<string> DoExtract(string mediaFilename, Argument arg) {
			try {
				var wavFilename = await AudioHelper.Extract(mediaFilename, arg);
				return wavFilename;
			} catch (Exception) {
				throw;
			}
		}

		private static async Task<KeyValuePair<string, string>> DoExtractMultithread(string mediaFilename, Argument arg) {
			try {
				var wavFilename = await AudioHelper.Extract(mediaFilename, arg);
				return new KeyValuePair<string, string>(mediaFilename, wavFilename);
			} catch (Exception ) {
				throw;
			}
		}

		private static async Task DoTranscribe(string wavFilename, string mediaFilename, int index, Argument arg) {
			try {
				await WhisperHelper.Transcribe(arg.ModelType, wavFilename, mediaFilename, arg.OutputDir, arg.Language);
				Output.Info($"Start transcribing media #{index}: {mediaFilename}");
				FileHelper.DelFile(wavFilename);
			} catch (Exception) {
				throw;
			}
		}

		private static async Task DoTranscribeMultithread(KeyValuePair<string, string>[] mediaWavMap, Argument arg) {
			try {
				/// Stress test mode
				//var tasks = Enumerable.Range(0, mediaWavMap.Length)
				//	.Select(i => Task.Run(() => DoTranscribe(mediaWavMap[i].Value, mediaWavMap[i].Key, i + 1, arg)));

				//await Task.WhenAll(tasks);

				int cnt = 1;
				foreach (var pair in mediaWavMap) {
					var wavFilename = pair.Value;
					var mediaFilename = pair.Key;
					await DoTranscribe(wavFilename, mediaFilename, cnt++, arg);
				}
			} catch (Exception) {
				throw;
			}
		}
	}
}
