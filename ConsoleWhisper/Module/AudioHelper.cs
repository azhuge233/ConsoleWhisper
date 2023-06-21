using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace ConsoleWhisper.Module {
	public static class AudioHelper {
		public static async Task<string> Extract(string mediaFilename) {
			try {
				var audioFilename = FileHelper.GetTempMp3File();

				var mediaInfo = await FFmpeg.GetMediaInfo(mediaFilename);

				int audioStreamIndex = GetAudioStreamIndex(mediaInfo.AudioStreams.ToList());
				var audioStream = mediaInfo.AudioStreams
					.Skip(audioStreamIndex)
					.FirstOrDefault();

				await DoConversion(audioStream, audioFilename);

				var resampledWaveFilename = Resample(audioFilename);
				FileHelper.DelFile(audioFilename);

				return resampledWaveFilename;
			} catch (Exception) {
				throw;
			}
		}

		private static async Task DoConversion(IAudioStream audioStream, string audioFilename) {
			var conversion = FFmpeg.Conversions.New();

			conversion.AddStream(audioStream)
				.SetOutputFormat(Format.mp3)
				.SetOutput(audioFilename);

			await conversion.Start();
		}

		private static string Resample(string audioFilename) {
			try {
				var newWaveFilename = FileHelper.GetTempWavFile();

				using var reader = new AudioFileReader(audioFilename);
				var outFormat = new WaveFormat(SampleRate, reader.WaveFormat.Channels);
				using var resampler = new MediaFoundationResampler(reader, outFormat);
				WaveFileWriter.CreateWaveFile(newWaveFilename, resampler);

				return newWaveFilename;
			} catch (Exception) {
				throw;
			}
		}

		private static int GetAudioStreamIndex(List<IAudioStream> audioStreams) {
			int index = 0, audioStreamCount = audioStreams.Count;

			if (audioStreamCount < 1) throw new NotSupportedException(message: "No audio stream detected.");
			else if (audioStreamCount > 1) {
				Output.Warn($"{audioStreams.First().Path}: Multiple audio stream detected.");
				for (int i = 0; i < audioStreamCount; i++)
					Output.Warn($"\t- {i}:\n\t\tLanguage: {audioStreams[i].Language} | Title: {audioStreams[i].Title}");

				Output.InfoR("Please specify the extracting audio stream index: ");
				index = Convert.ToInt32(Console.ReadLine());

				if (index > audioStreamCount - 1) throw new IndexOutOfRangeException(message: "Index is out of range.");
			}

			return index;
		}

		private const int SampleRate = 16000;
	}
}
