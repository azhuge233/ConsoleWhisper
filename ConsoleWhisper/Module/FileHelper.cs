﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleWhisper.Module {
	internal static class FileHelper {
		internal static readonly string ModelDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Model");

		internal static IEnumerable<string> ExpandFilePaths(IEnumerable<string> paths) {
			try {
				var fileList = new List<string>();

				foreach (var path in paths) {
					var substitutedArg = Environment.ExpandEnvironmentVariables(path);

					var dirPart = Path.GetDirectoryName(substitutedArg);
					if (dirPart.Length == 0)
						dirPart = ".";

					var filePart = Path.GetFileName(substitutedArg);

					foreach (var filepath in Directory.GetFiles(dirPart, filePart))
						fileList.Add(filepath);
				}

				return fileList;
			} catch (Exception) {
				throw;
			}
		}

		internal static void DelFile(string filePath) {
			File.Delete(filePath);
		}

		internal static string GetTranscriptPath(string outputDir, string filename) {
			var extension = Path.GetExtension(filename).TrimStart('.');
			var filenameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
			var transcriptName = Path.ChangeExtension($"{filenameWithoutExtension}-{extension}", SrtExtension);
			return Path.Combine(outputDir, transcriptName);
		}

		internal static string GetAudioPath(string outputDir, string filename) {
			var extension = Path.GetExtension(filename).TrimStart('.');
			var filenameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
			var transcriptName = Path.ChangeExtension($"{filenameWithoutExtension}-{extension}", Mp3Extension);
			return Path.Combine(outputDir, transcriptName);
		}

		#region Get temp file name
		internal static string GetTempFile() {
			return Path.GetTempFileName();
		}

		internal static string GetTempWavFile() {
			string tempFilename = GetTempFile();
			string waveFilename = Path.ChangeExtension(tempFilename, WaveExtension);
			DelFile(tempFilename);
			return waveFilename;
		}

		internal static string GetTempAacFile() {
			string tempFilename = GetTempFile();
			string waveFilename = Path.ChangeExtension(tempFilename, AacExtension);
			DelFile(tempFilename);
			return waveFilename;
		}

		internal static string GetTempMp3File() {
			string tempFilename = GetTempFile();
			string waveFilename = Path.ChangeExtension(tempFilename, Mp3Extension);
			DelFile(tempFilename);
			return waveFilename;
		}
		#endregion

		#region Whisper model related operations
		internal static bool ModelExists(string modelFilename) { 
			return File.Exists(Path.Combine(ModelDirectory, modelFilename));
		}

		internal static string GetModelName(string modelType) {
			return $"ggml-{modelType}.bin";
		}

		internal static string GetModelPath(string modelType) {
			return Path.Combine(ModelDirectory, modelType);
		}
		#endregion

		#region Add Text to Filestream
		internal static async Task AddText(FileStream fs, string value) {
			byte[] info = encoder.GetBytes(value);
			await fs.WriteAsync(info);
		}

		internal static async Task AddText(FileStream fs, int value) {
			byte[] info = encoder.GetBytes($"{value}\n");
			await fs.WriteAsync(info);
		}

		internal static async Task AddText(FileStream fs, TimeSpan value) {
			byte[] info = encoder.GetBytes(value.ToString("G"));
			await fs.WriteAsync(info);
		}
		#endregion

		private static readonly UTF8Encoding encoder = new(true);

		#region Extension string
		private const string WaveExtension = "wav";
		private const string AacExtension = "aac";
		private const string Mp3Extension = "mp3";
		private const string SrtExtension = "srt";
		#endregion
	}
}
