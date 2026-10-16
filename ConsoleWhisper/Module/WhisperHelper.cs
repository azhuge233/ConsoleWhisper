using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.LibraryLoader;

namespace ConsoleWhisper.Module {
	internal class WhisperHelper {
		internal static async Task Transcribe(WhisperFactory whisperFactory, string wavFilename, string mediaFilename, string outputDir, string outputBaseName, string language, CancellationToken cancellationToken) {
			await using var processor = whisperFactory.CreateBuilder()
				.WithLanguage(language)
				.WithPrintProgress()
				.Build();

			using var waveFileStream = File.OpenRead(wavFilename);

			var transcriptPath = FileHelper.GetTranscriptPath(outputDir, outputBaseName);
			var tempTranscriptPath = FileHelper.GetTempTranscriptPath(outputDir, outputBaseName);

			using (var transcriptFileStream = File.Create(tempTranscriptPath)) {
				Output.Info($"Start transcribing \"{mediaFilename}\"...");

				int cnt = 1;
				await foreach(var result in processor.ProcessAsync(waveFileStream, cancellationToken)) {
					await FileHelper.AddText(transcriptFileStream, cnt++);
					await FileHelper.AddText(transcriptFileStream, result.Start);
					await FileHelper.AddText(transcriptFileStream, " --> ");
					await FileHelper.AddText(transcriptFileStream, result.End);
					await FileHelper.AddText(transcriptFileStream, Environment.NewLine);
					await FileHelper.AddText(transcriptFileStream, result.Text);
					await FileHelper.AddText(transcriptFileStream, $"{Environment.NewLine}{Environment.NewLine}");
				}
			}

			// The transcript is moved into place only after the whole file was transcribed,
			// so a cancelled or failed run never leaves a partial .srt behind.
			cancellationToken.ThrowIfCancellationRequested();
			File.Move(tempTranscriptPath, transcriptPath, true);

			Output.Info($"Done: {Path.GetFileName(transcriptPath)}");
		}

		internal static async Task DownloadModel(string modelType, CancellationToken cancellationToken) {
			var modelPath = FileHelper.GetModelPath(modelType);
			var tempPath = FileHelper.GetTempModelPath(modelPath);

			// The application directory may not contain the (empty) model directory - a zipped
			// package for example loses it - so make sure it exists before writing into it.
			Directory.CreateDirectory(FileHelper.ModelDirectory);

			// Download to a temporary file first and move it into place afterwards,
			// so an interrupted download never leaves a half model behind.
			await using (var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ModelTypeMap[modelType], cancellationToken: cancellationToken))
			using (var fileWriter = File.Create(tempPath)) {
				var totalBytes = modelStream.CanSeek ? modelStream.Length : 0;
				var buffer = new byte[DownloadBufferSize];
				var downloadedBytes = 0L;
				int read;

				while ((read = await modelStream.ReadAsync(buffer, cancellationToken)) > 0) {
					await fileWriter.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
					downloadedBytes += read;
					Output.ReportDownload("Whisper model", downloadedBytes, totalBytes);
				}
			}

			Output.CompleteDownload("Whisper model");

			cancellationToken.ThrowIfCancellationRequested();
			File.Move(tempPath, modelPath, true);
		}

		/// <summary>
		/// Names the native library that was actually loaded. whisper.net silently falls back to the
		/// CPU runtime when a GPU one cannot be loaded, so the name must be reported as it is.
		/// </summary>
		internal static string FormatRuntimeName(RuntimeLibrary? loadedLibrary) {
			return loadedLibrary?.ToString() ?? NotLoadedLibraryName;
		}

		/// <summary>
		/// Reports the backend of the already loaded runtime - "not loaded" while no library was
		/// loaded at all (--only-extract never loads one).
		/// </summary>
		internal static string DescribeRuntime() {
			return FormatRuntimeName(RuntimeOptions.LoadedLibrary);
		}

		private const string NotLoadedLibraryName = "not loaded";
		private const int DownloadBufferSize = 1024 * 1024;

		private static readonly Dictionary<string, GgmlType> ModelTypeMap = new() {
			{ "ggml-base.bin", GgmlType.Base }, { "ggml-tiny.bin", GgmlType.Tiny }, { "ggml-small.bin", GgmlType.Small },
			{ "ggml-medium.bin", GgmlType.Medium }, { "ggml-large-v1.bin", GgmlType.LargeV1 }, { "ggml-large-v2.bin", GgmlType.LargeV2 },
			{ "ggml-large-v3.bin", GgmlType.LargeV3 }, { "ggml-large-v3-turbo.bin", GgmlType.LargeV3Turbo }
		};
	}
}
