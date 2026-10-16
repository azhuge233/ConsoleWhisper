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
using Whisper.net.LibraryLoader;

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

					ReportRuntime(arg, whisperFactory);

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

		/// <summary>
		/// Reports the native backend that serves this run. The GPU packages fall back to the CPU
		/// runtime without saying so, and only this line tells the user what really runs.
		/// </summary>
		private static void ReportRuntime(Argument arg, WhisperFactory? whisperFactory) {
			Output.Info($"Runtime: {WhisperHelper.DescribeRuntime()}");

			// The capability text needs a loaded native library, so --only-extract has none to show.
			if (!arg.Verbose || whisperFactory is null)
				return;

			// The capability text is informational only - up there it comes from a native call that may
			// throw, and a diagnostic line must never be able to abort a whole batch.
			string runtimeInfo;
			try {
				runtimeInfo = WhisperFactory.GetRuntimeInfo() ?? NoRuntimeInfoName;
			} catch (Exception) {
				runtimeInfo = NoRuntimeInfoName;
			}

			Output.Info($"Runtime info: {runtimeInfo}");
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
			var pendingFiles = extractions.Where(extraction => extraction.WavFilename is not null).ToList();

			// Only the transcription phase runs several files at once, and only when a plan says so (see
			// ResolveTranscriptionPlan): a single file or a CPU backend keeps the one-at-a-time path this
			// program always had.
			var plan = ResolveTranscriptionPlan(pendingFiles.Count, arg);
			int succeeded;

			if (plan is null) {
				succeeded = 0;

				foreach (var extraction in pendingFiles) {
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
			} else {
				succeeded = await TranscribeInParallel(pendingFiles, arg, whisperFactory, plan, failures, cancellationToken);
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

		/// <summary>
		/// Transcribes one extracted file. A plan makes it part of a parallel run (its own share of the
		/// cores, per-file output lines); without one it behaves exactly like the serial path.
		/// </summary>
		private static async Task TranscribeExtractedFile(ExtractionResult extraction, Argument arg, WhisperFactory? whisperFactory, CancellationToken cancellationToken, TranscriptionPlan? plan = null) {
			if (arg.OnlyExtract)
				return; // extracted audio is the final output, keep it

			try {
				// Only successful extractions reach this method, so the extracted file and the
				// factory are both present.
				await WhisperHelper.Transcribe(whisperFactory!, extraction.WavFilename!, extraction.Job.MediaFilename, extraction.Job.OutputDir, extraction.Job.OutputBaseName, arg.Language, cancellationToken,
					threads: plan?.Threads, parallel: plan is not null);
			} finally {
				FileHelper.DelFile(extraction.WavFilename);
			}
		}

		/// <summary>
		/// Transcribes the files that came out of the extraction phase with several files in flight at
		/// once. Every file keeps the failure isolation of the one-at-a-time path: a failure is reported
		/// and collected on its own, so one bad file never takes the rest of the batch down.
		/// </summary>
		private static async Task<int> TranscribeInParallel(List<ExtractionResult> pendingFiles, Argument arg, WhisperFactory? whisperFactory, TranscriptionPlan plan, List<(string MediaFilename, string? Message)> failures, CancellationToken cancellationToken) {
			var succeeded = 0;
			var failuresLock = new object();

			// One slot per file that really runs at once: the files beyond it wait their turn, so even a
			// large batch never holds more transcriptions (and more memory) than the plan allows.
			using var transcriptionSlots = new SemaphoreSlim(plan.Concurrency);

			await Task.WhenAll(pendingFiles.Select(async extraction => {
				await transcriptionSlots.WaitAsync(cancellationToken);

				try {
					await TranscribeExtractedFile(extraction, arg, whisperFactory, cancellationToken, plan);
					Interlocked.Increment(ref succeeded);
				} catch (OperationCanceledException) {
					throw;
				} catch (Exception ex) {
					ReportSingleFailure(extraction.Job.MediaFilename, ex);

					// Several files can fail at the same time, and the list is shared with the rest of the run.
					lock (failuresLock)
						failures.Add((extraction.Job.MediaFilename, ex.Message));
				} finally {
					transcriptionSlots.Release();
				}
			}));

			return succeeded;
		}

		/// <summary>
		/// Resolves how the transcription phase runs: a plan means several files are transcribed at the
		/// same time, no plan means the classic one-file-at-a-time path.
		/// </summary>
		private static TranscriptionPlan? ResolveTranscriptionPlan(int pendingFiles, Argument arg) {
			if (arg.OnlyExtract)
				return null; // nothing is transcribed at all

			var processorCount = Environment.ProcessorCount;
			var concurrency = GetTranscriptionConcurrency(processorCount);

			if (!ShouldTranscribeInParallel(pendingFiles, concurrency, RuntimeOptions.LoadedLibrary)) {
				ExplainSerialTranscription(arg, pendingFiles, concurrency);
				return null;
			}

			// The threads are handed out by the planned concurrency, not by the files that happen to be
			// left: a batch that lost most of its extractions still gets the same threads per file.
			return new TranscriptionPlan(GetActiveTranscriptionSlots(concurrency, pendingFiles), GetThreadsPerTranscriber(processorCount, concurrency));
		}

		/// <summary>
		/// Explains, on demand (--verbose), why a multi-file run transcribes one file at a time after
		/// all: on a CPU backend that is intended behaviour rather than a failed parallel run.
		/// </summary>
		private static void ExplainSerialTranscription(Argument arg, int pendingFiles, int concurrency) {
			if (!arg.Verbose || pendingFiles <= 1 || concurrency <= 1)
				return;

			// With several files and room for several transcriptions, the backend is the only reason left. The
			// concurrency check is defensive only: the clamp floor keeps the concurrency at 2 or more.
			Output.Info($"Transcribing one file at a time: the {WhisperHelper.DescribeRuntime()} backend does not benefit from parallel transcription.");
		}

		/// <summary>
		/// Files a parallel run transcribes at once: a quarter of the cores, kept between two and four.
		/// The lower bound of two is deliberate and not a correction of the core count - on a machine with
		/// one or two cores the concurrency is larger than the core count, and the CPU backend check plus
		/// the pending file count keep that ("two files, one thread each") harmless.
		/// </summary>
		internal static int GetTranscriptionConcurrency(int processorCount) {
			return Math.Clamp(processorCount / CoresPerTranscription, MinParallelTranscriptions, MaxParallelTranscriptions);
		}

		/// <summary>
		/// Threads one transcription of a parallel run may use: the cores are shared out between the files
		/// running at once, so the instances do not all ask for every core.
		/// </summary>
		internal static int GetThreadsPerTranscriber(int processorCount, int concurrency) {
			return Math.Max(1, processorCount / concurrency);
		}

		/// <summary>
		/// Files that really run at once: a parallel run never starts more transcriptions than there are
		/// files waiting for one.
		/// </summary>
		internal static int GetActiveTranscriptionSlots(int concurrency, int pendingFiles) {
			return Math.Min(concurrency, pendingFiles);
		}

		/// <summary>
		/// Whether the transcription phase goes parallel: it takes more than one file, room for more than
		/// one transcription and a backend that can take several of them at once.
		/// </summary>
		internal static bool ShouldTranscribeInParallel(int pendingFiles, int concurrency, RuntimeLibrary? loadedLibrary) {
			return pendingFiles > 1 && concurrency > 1 && IsGpuBackend(loadedLibrary);
		}

		/// <summary>
		/// Every backend except the CPU runtimes counts as one that gains from parallel transcriptions:
		/// the CPU runtimes are already saturated by a single instance, so extra ones only add context
		/// switching (measured 1.16x there, while the memory use doubles), while CUDA and Vulkan both leave
		/// the GPU with room for several. A missing library (nothing loaded at all, e.g. --only-extract) is
		/// no GPU either.
		/// </summary>
		internal static bool IsGpuBackend(RuntimeLibrary? loadedLibrary) {
			return loadedLibrary is not null and not RuntimeLibrary.Cpu and not RuntimeLibrary.CpuNoAvx;
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

		/// <summary>
		/// Settings of a parallel transcription phase: how many files run at once and how many threads
		/// each of them may use. No plan means the one-file-at-a-time path.
		/// </summary>
		private sealed record TranscriptionPlan(int Concurrency, int Threads);

		private static readonly TimeSpan ForceQuitHintDelay = TimeSpan.FromSeconds(1.5);
		private const int FallbackDisplayWidth = 80;
		private const string NoRuntimeInfoName = "unavailable";
		private static readonly int MaxParallelExtractions = Math.Max(1, Environment.ProcessorCount / 2);

		/// <summary>
		/// Cores per parallel transcription (a quarter of them) and the bounds that number is kept in: the
		/// measurements behind the parallel mode put the sweet spot at three to four files at once (two
		/// files gain little), and the floor keeps a machine with few cores at two files.
		/// </summary>
		private const int CoresPerTranscription = 4;
		private const int MinParallelTranscriptions = 2;
		private const int MaxParallelTranscriptions = 4;
	}
}
