using ConsoleWhisper.Module;
using Whisper.net.LibraryLoader;

namespace ConsoleWhisper.Tests {
	[TestClass]
	public class ParallelTranscriptionTests {
		[TestMethod]
		public void TranscriptionConcurrencyIsAQuarterOfTheCoresClampedBetweenTwoAndFour() {
			Assert.AreEqual(2, Runner.GetTranscriptionConcurrency(1));
			Assert.AreEqual(2, Runner.GetTranscriptionConcurrency(4));
			Assert.AreEqual(2, Runner.GetTranscriptionConcurrency(8));
			Assert.AreEqual(3, Runner.GetTranscriptionConcurrency(12));
			Assert.AreEqual(4, Runner.GetTranscriptionConcurrency(16));
			Assert.AreEqual(4, Runner.GetTranscriptionConcurrency(64));
		}

		[TestMethod]
		public void ThreadsPerTranscriberShareTheCoresAndNeverDropBelowOne() {
			Assert.AreEqual(4, Runner.GetThreadsPerTranscriber(12, 3));
			Assert.AreEqual(3, Runner.GetThreadsPerTranscriber(12, 4));
			Assert.AreEqual(2, Runner.GetThreadsPerTranscriber(8, 4));
			Assert.AreEqual(1, Runner.GetThreadsPerTranscriber(2, 2));
			Assert.AreEqual(1, Runner.GetThreadsPerTranscriber(1, 2)); // fewer cores than files
		}

		[TestMethod]
		public void ActiveSlotsNeverExceedTheNumberOfFilesToTranscribe() {
			Assert.AreEqual(2, Runner.GetActiveTranscriptionSlots(3, 2));
			Assert.AreEqual(3, Runner.GetActiveTranscriptionSlots(3, 3));
			Assert.AreEqual(3, Runner.GetActiveTranscriptionSlots(3, 5));
			Assert.AreEqual(1, Runner.GetActiveTranscriptionSlots(4, 1));
		}

		[TestMethod]
		public void CpuBackendKeepsTheTranscriptionSerial() {
			Assert.IsFalse(Runner.ShouldTranscribeInParallel(3, 3, RuntimeLibrary.Cpu));
			Assert.IsFalse(Runner.ShouldTranscribeInParallel(3, 3, RuntimeLibrary.CpuNoAvx));
			Assert.IsFalse(Runner.ShouldTranscribeInParallel(3, 3, null));
			Assert.IsFalse(Runner.IsGpuBackend(RuntimeLibrary.Cpu));
			Assert.IsFalse(Runner.IsGpuBackend(RuntimeLibrary.CpuNoAvx));
			Assert.IsFalse(Runner.IsGpuBackend(null));
		}

		[TestMethod]
		public void ParallelTranscriptionNeedsAGpuBackendSeveralFilesAndRealConcurrency() {
			Assert.IsTrue(Runner.ShouldTranscribeInParallel(2, 3, RuntimeLibrary.Cuda));
			Assert.IsTrue(Runner.ShouldTranscribeInParallel(5, 4, RuntimeLibrary.Vulkan));
			Assert.IsTrue(Runner.IsGpuBackend(RuntimeLibrary.Cuda));
			Assert.IsFalse(Runner.ShouldTranscribeInParallel(1, 3, RuntimeLibrary.Cuda)); // a single file
			Assert.IsFalse(Runner.ShouldTranscribeInParallel(0, 3, RuntimeLibrary.Cuda)); // nothing left to transcribe
			Assert.IsFalse(Runner.ShouldTranscribeInParallel(3, 1, RuntimeLibrary.Cuda)); // no room for a second instance
		}

		[TestMethod]
		public void ParallelLinesArePrefixedWithTheFileNameAndFormedInOnePiece() {
			const string startLine = "Start transcribing \"D:\\videos\\Episode 1.mkv\"...";

			Assert.AreEqual($"[Episode 1.mkv] {startLine}",
				WhisperHelper.FormatLine(WhisperHelper.FormatPrefix(true, @"D:\videos\Episode 1.mkv"), startLine));
			Assert.AreEqual("Done: episode-1-mkv.srt",
				WhisperHelper.FormatLine(WhisperHelper.FormatPrefix(false, @"D:\videos\Episode 1.mkv"), "Done: episode-1-mkv.srt"));
		}
	}
}
