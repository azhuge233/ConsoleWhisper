using ConsoleWhisper.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace ConsoleWhisper.Module {
	public static class AudioHelper {
		public static async Task<string> Extract(string mediaFilename, string outputDir, string outputBaseName, Argument arg, CancellationToken cancellationToken) {
			var finalAudioPath = arg.OnlyExtract ? FileHelper.GetAudioPath(outputDir, outputBaseName) : null;
			var audioFilename = finalAudioPath is null ? FileHelper.GetTempWavFile() : FileHelper.GetTempAudioPath(outputDir, outputBaseName);

			var mediaInfo = await FFmpeg.GetMediaInfo(mediaFilename);

			var audioStreams = mediaInfo.AudioStreams.ToList();
			var audioStreamIndex = SelectAudioStreamIndex(mediaFilename, audioStreams, arg, cancellationToken);
			var audioStream = audioStreams[audioStreamIndex];
			Output.Info($"Using audio stream #{audioStreamIndex} ({DescribeStream(audioStream)})");

			Output.Info($"Extracting audio to {Path.GetFileName(finalAudioPath ?? audioFilename)}");

			// Single ffmpeg pass: stream selection, extraction and (for transcription)
			// the resampling whisper expects, all in one conversion.
			await ConvertToWav(audioStream, audioFilename, resampleForWhisper: finalAudioPath is null, cancellationToken);

			if (finalAudioPath is null)
				return audioFilename;

			File.Move(audioFilename, finalAudioPath, true);
			return finalAudioPath;
		}

		private static async Task ConvertToWav(IAudioStream audioStream, string audioFilename, bool resampleForWhisper, CancellationToken cancellationToken) {
			var conversion = FFmpeg.Conversions.New();

			conversion.AddStream(audioStream)
				.SetOutputFormat(Format.wav)
				.SetOutput(audioFilename)
				.SetOverwriteOutput(true);

			if (resampleForWhisper)
				conversion.AddParameter($"-ar {SampleRate} -ac 1");

			await conversion.Start(cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
		}

		private static int SelectAudioStreamIndex(string mediaFilename, List<IAudioStream> audioStreams, Argument arg, CancellationToken cancellationToken) {
			if (audioStreams.Count == 0)
				throw new NotSupportedException(message: $"No audio stream detected in \"{mediaFilename}\".");

			if (arg.AudioIndex is { } requestedIndex)
				return ValidateAudioStreamIndex(requestedIndex, audioStreams);

			if (!string.IsNullOrWhiteSpace(arg.AudioLang))
				return FindAudioStreamByLanguage(mediaFilename, arg.AudioLang, audioStreams);

			// Multithread mode never prompts: it always uses the first audio stream.
			if (arg.Multithread || audioStreams.Count == 1)
				return 0;

			if (Console.IsInputRedirected)
				throw new NotSupportedException(message:
					$"\"{mediaFilename}\": multiple audio streams detected ({DescribeAudioStreams(audioStreams)}), " +
					"but the program runs without an interactive terminal.\n" +
					"Specify the audio stream to use with \"--audio-index <n>\" or \"--audio-lang <code>\".");

			return PromptForAudioStreamIndex(mediaFilename, audioStreams, cancellationToken);
		}

		private static int ValidateAudioStreamIndex(int index, List<IAudioStream> audioStreams) {
			if (index >= audioStreams.Count)
				throw new ArgumentException(message: $"Audio stream index {index} is out of range, available: 0..{audioStreams.Count - 1} ({DescribeAudioStreams(audioStreams)}).");

			return index;
		}

		private static int FindAudioStreamByLanguage(string mediaFilename, string requestedLanguage, List<IAudioStream> audioStreams) {
			var matchIndex = AudioLanguage.FindMatch(requestedLanguage, audioStreams.ConvertAll(stream => stream.Language));

			if (matchIndex >= 0)
				return matchIndex;

			// Nothing matched: a file with a single audio stream leaves no choice, so use it -
			// with several streams we cannot tell which one was meant, so fail instead of guessing.
			if (audioStreams.Count == 1) {
				Output.Warn($"{mediaFilename}: no audio stream matches language \"{requestedLanguage}\", using the only available audio stream ({DescribeStream(audioStreams[0])}).");
				return 0;
			}

			throw new ArgumentException(message:
				$"No audio stream matches language \"{requestedLanguage}\", available: {DescribeAudioStreams(audioStreams)}.");
		}

		private static int PromptForAudioStreamIndex(string mediaFilename, List<IAudioStream> audioStreams, CancellationToken cancellationToken) {
			if (audioStreams.Count > 1) {
				Output.Warn($"{mediaFilename}: Multiple audio stream detected.");
				for (var index = 0; index < audioStreams.Count; index++)
					Output.Warn($"\t- {index}:\n\t\t{DescribeStream(audioStreams[index])}");
			}

			for (var attempt = 0; attempt < MaxPromptAttempts; attempt++) {
				Output.InfoR($"Please specify the extracting audio stream index (0-{audioStreams.Count - 1}): ");
				var input = Console.ReadLine();

				// null means no more input: either the stream ended or the user pressed Ctrl+C.
				if (input is null)
					cancellationToken.ThrowIfCancellationRequested();

				if (input is not null && int.TryParse(input.Trim(), out var index) && index >= 0 && index < audioStreams.Count)
					return index;

				Output.Warn("Invalid input.");
			}

			throw new ArgumentException(message: "No valid audio stream index was given.");
		}

		private static string DescribeAudioStreams(List<IAudioStream> audioStreams) =>
			string.Join(", ", audioStreams.Select((stream, index) => $"#{index}: {DescribeStream(stream)}"));

		private static string DescribeStream(IAudioStream stream) {
			var language = string.IsNullOrWhiteSpace(stream.Language) ? "unknown language" : stream.Language;
			var title = string.IsNullOrWhiteSpace(stream.Title) ? string.Empty : $" | {stream.Title}";

			return $"language: {language}{title}";
		}

		private const int SampleRate = 16000;
		private const int MaxPromptAttempts = 3;
	}
}
