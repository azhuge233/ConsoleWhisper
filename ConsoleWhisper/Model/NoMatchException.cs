using System;

namespace ConsoleWhisper.Model {
	internal class NoMatchException : Exception {
		internal NoMatchException(string message) : base(message) { }
	}
}
