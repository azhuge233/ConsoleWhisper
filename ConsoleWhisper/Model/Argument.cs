using CommandLine;
using System;
using System.Collections.Generic;

namespace ConsoleWhisper.Model {
	public class Argument {
		[Option('i', "input", Required = true, Hidden = false, Separator = ' ', HelpText = "Input media files.")]
		[Value(3)]
		public IEnumerable<string> Files { get; set; }

		[Option('m', "model", Required = false, Hidden = false, Default = "small", HelpText = "Whisper model: base, tiny, small, medium, large.")]
		[Value(0, Min = 0, Max = 1, Required = false)]
		public string ModelType { get; set; }

		[Option('o', "output", Required = false, Hidden = false, HelpText = "Output directory.")]
		[Value(1, Min = 0, Max = 1, Required = false)]
		public string OutputDir { get; set; }

		[Option('g', "gpu", Required = false, Hidden = true, HelpText = "Currently not implemented.")]
		[Value(2, Required = false, Default = false)]
		public bool GPU { get; set; }

		public Argument() {
			OutputDir = Environment.CurrentDirectory;
		}

		public void Validate() {
			if (!SupportedModels.Contains(ModelType))
				throw new ArgumentException(message: $"Whisper model type \"{ModelType}\" is not supported.");
		}

		internal const int SupportedArgumentsCount = 5;

		private static readonly HashSet<string> SupportedModels = new() { "base", "tiny", "small", "medium", "large" };
	}
}
