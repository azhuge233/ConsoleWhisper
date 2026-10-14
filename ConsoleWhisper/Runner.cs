using CommandLine.Text;
using CommandLine;
using ConsoleWhisper.Model;
using ConsoleWhisper.Module;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;
using Whisper.net;

namespace ConsoleWhisper {
	internal static class Runner {
		internal static async Task<int> Run(Argument arg) {
			arg.Validate();

			arg.ModelType = FileHelper.GetModelName(arg.ModelType);
			// Resolve every output name up front, in the (sorted) input order: doing it lazily
			// inside the parallel extraction would make the -2 / -3 assignment depend on timing.
			var jobs = FileHelper.ExpandFilePaths(arg.Files)
				.Select(mediaFilename => {
					var outputDir = FileHelper.ResolveOutputDir(arg.OutputDir, mediaFilename);
					return new MediaJob(mediaFilename, outputDir, FileHelper.GetOutputBaseName(outputDir, mediaFilename));
				})
				.ToList();

			using var cancellation = new CancellationTokenSource();
			using var forceQuitHint = new CancellationTokenSource();
			ConsoleCancelEventHandler cancelHandler = (_, eventArgs) => {
				if (cancellation.IsCancellationRequested)
					return;

				eventArgs.Cancel = true;
				Output.Warn("Cancelling...");
				cancellation.Cancel();
				_ = ShowForceQuitHint(forceQuitHint.Token, ForceQuitHintDelay, () => Output.Warn("Still cancelling, press Ctrl+C again to force quit."));
			};
			Console.CancelKeyPress += cancelHandler;

			try {
				await FileHelper.PrepareFFmpeg();

				WhisperFactory? whisperFactory = null;
				try {
					if (!arg.OnlyExtract) {
						await FileHelper.PrepareModel(arg.ModelType, cancellation.Token);
						whisperFactory = WhisperFactory.FromPath(FileHelper.GetModelPath(arg.ModelType));
					}

					return arg.Multithread
						? await DoMultithread(jobs, arg, whisperFactory, cancellation.Token)
						: await DoSinglethread(jobs, arg, whisperFactory, cancellation.Token);
				} finally {
					whisperFactory?.Dispose();
				}
			} catch (OperationCanceledException) {
				Output.Warn("Cancelled.");
				return ExitCode.Cancelled;
			} finally {
				// The run is over, so the "still cancelling" hint is no longer useful.
				forceQuitHint.Cancel();

				// Clean up before unhooking the handler: if the user presses Ctrl+C during the
				// cleanup we want it to be seen as an ordinary (already cancelled) signal.
				TempFiles.Cleanup();
				Console.CancelKeyPress -= cancelHandler;
			}
		}

		internal static int Error<T>(ParserResult<T> result, List<Error> errs) {
			var helpText = HelpText.AutoBuild(result, options => {
				options.AdditionalNewLineAfterOption = false;
				options.MaximumDisplayWidth = GetDisplayWidth();
				options.AutoHelp = true;
				options.AutoVersion = true;
				return HelpText.DefaultParsingErrorsHandler(result, options);
			}, e => e);

			Output.Help(helpText, errs.IsVersion());

			return errs.IsHelp() || errs.IsVersion() ? ExitCode.Success : ExitCode.Fatal;
		}

		private static async Task<int> DoSinglethread(List<MediaJob> jobs, Argument arg, WhisperFactory? whisperFactory, CancellationToken cancellationToken) {
			var failures = new List<(string MediaFilename, string? Message)>();
			var succeeded = 0;

			foreach (var job in jobs) {
				cancellationToken.ThrowIfCancellationRequested();

				try {
					await ProcessJob(job, arg, whisperFactory, cancellationToken);
					succeeded++;
				} catch (OperationCanceledException) {
					throw;
				} catch (Exception ex) {
					ReportSingleFailure(job.MediaFilename, ex);
					failures.Add((job.MediaFilename, ex.Message));
				}
			}

			return ReportSummary(succeeded, failures, jobs.Count, arg.OnlyExtract);
		}

		private static async Task<int> DoMultithread(List<MediaJob> jobs, Argument arg, WhisperFactory? whisperFactory, CancellationToken cancellationToken) {
			if (!arg.Yes) {
				if (Console.IsInputRedirected)
					throw new NotSupportedException(message:
						"Multithread mode asks for a confirmation, but the program runs without an interactive terminal.\n" +
						"Use \"-y\" / \"--yes\" to skip the confirmation.");

				Output.WarnR($"Using multithreading disables user input.\n" +
						$"If a media file has multiple soundtracks, the first one is extracted automatically.\n" +
						$"Make sure there is enough disk space for the temporary .wav files.\n" +
						$"Proceed? yes(y)/No(N): ");

				var confirm = Console.ReadLine()?.Trim();

				if (confirm is null)
					cancellationToken.ThrowIfCancellationRequested();

				if (!string.Equals(confirm, "y", StringComparison.OrdinalIgnoreCase) && !string.Equals(confirm, "yes", StringComparison.OrdinalIgnoreCase)) {
					Output.Error("Aborted.");
					return ExitCode.Cancelled;
				}
			}

			// Extraction runs ahead of transcription, so keep the number of concurrent ffmpeg
			// processes (and of temp .wav files) bounded instead of starting one per input file.
			using var extractionSlots = new SemaphoreSlim(MaxParallelExtractions);

			var extractions = await Task.WhenAll(jobs.Select(async job => {
				await extractionSlots.WaitAsync(cancellationToken);

				try {
					return await ExtractIsolated(job, arg, cancellationToken);
				} finally {
					extractionSlots.Release();
				}
			}));

			var failures = extractions
				.Where(extraction => extraction.WavFilename is null)
				.Select(extraction => (extraction.Job.MediaFilename, extraction.ErrorMessage))
				.ToList();
			var succeeded = 0;

			foreach (var extraction in extractions.Where(extraction => extraction.WavFilename is not null)) {
				cancellationToken.ThrowIfCancellationRequested();

				try {
					await TranscribeExtractedFile(extraction, arg, whisperFactory, cancellationToken);
					succeeded++;
				} catch (OperationCanceledException) {
					throw;
				} catch (Exception ex) {
					ReportSingleFailure(extraction.Job.MediaFilename, ex);
					failures.Add((extraction.Job.MediaFilename, ex.Message));
				}
			}

			return ReportSummary(succeeded, failures, jobs.Count, arg.OnlyExtract);
		}

		private static async Task ProcessJob(MediaJob job, Argument arg, WhisperFactory? whisperFactory, CancellationToken cancellationToken) {
			var wavFilename = await AudioHelper.Extract(job.MediaFilename, job.OutputDir, job.OutputBaseName, arg, cancellationToken);

			if (arg.OnlyExtract)
				return; // extracted audio is the final output, keep it

			try {
				// The factory is created whenever --only-extract is off (see Run).
				await WhisperHelper.Transcribe(whisperFactory!, wavFilename, job.MediaFilename, job.OutputDir, job.OutputBaseName, arg.Language, cancellationToken);
			} finally {
				FileHelper.DelFile(wavFilename);
			}
		}

		private static async Task<ExtractionResult> ExtractIsolated(MediaJob job, Argument arg, CancellationToken cancellationToken) {
			try {
				var wavFilename = await AudioHelper.Extract(job.MediaFilename, job.OutputDir, job.OutputBaseName, arg, cancellationToken);

				return new ExtractionResult(job, wavFilename, null);
			} catch (OperationCanceledException) {
				throw;
			} catch (Exception ex) {
				ReportSingleFailure(job.MediaFilename, ex);
				return new ExtractionResult(job, null, ex.Message);
			}
		}

		private static async Task TranscribeExtractedFile(ExtractionResult extraction, Argument arg, WhisperFactory? whisperFactory, CancellationToken cancellationToken) {
			if (arg.OnlyExtract)
				return; // extracted audio is the final output, keep it

			try {
				// Only successful extractions reach this method, so the extracted file and the
				// factory are both present.
				await WhisperHelper.Transcribe(whisperFactory!, extraction.WavFilename!, extraction.Job.MediaFilename, extraction.Job.OutputDir, extraction.Job.OutputBaseName, arg.Language, cancellationToken);
			} finally {
				FileHelper.DelFile(extraction.WavFilename);
			}
		}

		private static void ReportSingleFailure(string mediaFilename, Exception exception) {
			Output.Error($"Failed: \"{mediaFilename}\" - {exception.Message}");
		}

		/// <summary>
		/// Shows a hint about the force-quit keystroke, but only when the graceful cancellation
		/// drags on. Downloads, ffmpeg and the transcriber all honour the token at once, while the
		/// ffmpeg binary download cannot be cancelled at all - that is the case this hint is for.
		/// </summary>
		internal static async Task ShowForceQuitHint(CancellationToken hintCancellation, TimeSpan delay, Action hint) {
			try {
				await Task.Delay(delay, hintCancellation);
			} catch (OperationCanceledException) {
				return;
			}

			hint();
		}

		private static int ReportSummary(int succeeded, List<(string MediaFilename, string? Message)> failures, int total, bool onlyExtract) {
			var action = onlyExtract ? "extracted" : "transcribed";

			if (failures.Count == 0) {
				Output.Success($"All done: {succeeded}/{total} file(s) {action}.");
				return ExitCode.Success;
			}

			Output.Error($"Finished with failures: {succeeded} succeeded, {failures.Count} failed, {total} total.");
			foreach (var (mediaFilename, _) in failures)
				Output.Error($"  - \"{mediaFilename}\"");

			return ExitCode.PartialFailure;
		}

		private static int GetDisplayWidth() {
			// Console.WindowWidth throws when the output is redirected (no console), e.g. when the
			// help text is piped into a file - fall back to a readable default width.
			try {
				var width = Console.WindowWidth;
				return width > 0 ? width : FallbackDisplayWidth;
			} catch (Exception) {
				return FallbackDisplayWidth;
			}
		}

		private sealed record MediaJob(string MediaFilename, string OutputDir, string OutputBaseName);

		private sealed record ExtractionResult(MediaJob Job, string? WavFilename, string? ErrorMessage);

		private static readonly TimeSpan ForceQuitHintDelay = TimeSpan.FromSeconds(1.5);
		private const int FallbackDisplayWidth = 80;
		private static readonly int MaxParallelExtractions = Math.Max(1, Environment.ProcessorCount / 2);
	}
}
