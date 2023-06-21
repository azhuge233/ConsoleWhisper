using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;

namespace ConsoleWhisper.Module {
	internal class WhisperHelper {
		internal static async Task Transcribe(string modelType, string wavFilename, string mediaFilename, string outputDir) {
			try {
				using var whisperFactory = WhisperFactory.FromPath(FileHelper.GetModelPath(modelType));

				using var processor = whisperFactory.CreateBuilder()
					.WithLanguage("auto")
					.WithPrintProgress()
					.Build();

				using var waveFileStream = File.OpenRead(wavFilename);
				using var transcriptFileStream = File.OpenWrite(FileHelper.GetTranscriptPath(outputDir, mediaFilename));

				int cnt = 1;
				await foreach(var result in processor.ProcessAsync(waveFileStream)) {
					await FileHelper.AddText(transcriptFileStream, cnt++);
					await FileHelper.AddText(transcriptFileStream, result.Start);
					await FileHelper.AddText(transcriptFileStream, " --> ");
					await FileHelper.AddText(transcriptFileStream, result.End);
					await FileHelper.AddText(transcriptFileStream, "\n");
					await FileHelper.AddText(transcriptFileStream, result.Text);
					await FileHelper.AddText(transcriptFileStream, "\n\n");
				}

			} catch (Exception) {
				throw;
			}
		}
		internal static async Task DownloadModel(string modelType) {
			try {
				using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(ModelTypeMap[modelType]);
				using var fileWriter = File.OpenWrite(FileHelper.GetModelPath(modelType));
				await modelStream.CopyToAsync(fileWriter);
			} catch (Exception) {
				throw;
			}
		}

		private static readonly Dictionary<string, GgmlType> ModelTypeMap = new() {
			{ "ggml-base.bin", GgmlType.Base }, { "ggml-tiny.bin", GgmlType.Tiny }, { "ggml-small.bin", GgmlType.Small },
			{ "ggml-medium.bin", GgmlType.Medium }, { "ggml-large.bin", GgmlType.Large }
		};
	}
}
