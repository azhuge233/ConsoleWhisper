using CommandLine;
using System;
using System.Collections.Generic;

namespace ConsoleWhisper.Model {
	public class Argument {
		[Option('i', "input", Required = true, Hidden = false, Separator = ',', HelpText = "Input media files.")]
		[Value(5)]
		public IEnumerable<string> Files { get; set; }

		[Option('m', "model", Required = false, Hidden = false, Default = "small", HelpText = "Whisper model: base, tiny, small, medium, large.")]
		[Value(0, Min = 0, Max = 1, Required = false)]
		public string ModelType { get; set; }

		[Option('o', "output", Required = false, Hidden = false, HelpText = "(Default: current directory) Output directory.")]
		[Value(1, Min = 0, Max = 1, Required = false)]
		public string OutputDir { get; set; }

		[Option('l', "language", Required = false, Hidden = false, Default = "auto", HelpText = "Specify transcribe language.")]
		[Value(2, Required = false)]
		public string Language { get; set; }

		[Option("only-extract", Required = false, Hidden = false, Default = false, HelpText = "Extract audio stream only.")]
		[Value(3, Required = false)]
		public bool OnlyExtract { get; set; }

		[Option("multithread", Required = false, Hidden = false, Default = false, HelpText = "Use multithread, only works for audio extraction.")]
		[Value(4, Required = false)]
		public bool Multithread { get; set; }

		//[Option('g', "gpu", Required = false, Hidden = true, HelpText = "Currently not implemented.")]
		//[Value(2, Required = false, Default = false)]
		//public bool GPU { get; set; }

		public Argument() {
			OutputDir = Environment.CurrentDirectory;
		}

		public void Validate() {
			if (!SupportedModels.Contains(ModelType))
				throw new ArgumentException(message: $"Whisper model type \"{ModelType}\" is not supported.");
			if (!SupportedLanguages.Contains(Language))
				throw new ArgumentException(message: $"Language \"{Language}\" is not supported.\nCheck {LanguageLink} for available languages.");
		}

		internal const int SupportedArgumentsCount = 8;

		private static readonly HashSet<string> SupportedModels = new() { "base", "tiny", "small", "medium", "large" };
		private static readonly HashSet<string> SupportedLanguages = new() { "en", "zh", "de", "es", "ru", "ko", "fr", "ja", "pt", "tr",
			"pl", "ca", "nl", "ar", "sv", "it", "id", "hi", "fi", "vi", "he", "uk", "el", "ms", "ro", "da", "hu", "ta", "no", "th", "ur",
			"hr", "bg", "lt", "la", "mi", "ml", "cy", "sk", "te", "fa", "lv", "bn", "sr", "az", "sl", "kn", "et", "mk", "br", "eu", "is",
			"hy", "ne", "mn", "bs", "kk", "sq", "sw", "gl", "mr", "pa", "si", "km", "sn", "yo", "so", "af", "oc", "ka", "be", "tg", "sd",
			"gu", "am", "yi", "lo", "uz", "fo", "ht", "ps", "tk", "nn", "mt", "sa", "lb", "my", "bo", "tl", "mg", "as", "tt", "haw", "ln",
			"ha", "ba", "jw", "su", "auto" };

		private const string LanguageLink = "https://github.com/ggerganov/whisper.cpp/blob/57543c169e27312e7546d07ed0d8c6eb806ebc36/whisper.cpp#L121";
	}
}
