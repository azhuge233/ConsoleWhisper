namespace ConsoleWhisper {
	internal static class ExitCode {
		internal const int Success = 0;
		internal const int Fatal = 1;
		internal const int NoMatch = 2;
		internal const int PartialFailure = 3;
		internal const int Cancelled = 130;
	}
}
