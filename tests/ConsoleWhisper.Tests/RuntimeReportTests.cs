using ConsoleWhisper.Module;
using Whisper.net.LibraryLoader;

namespace ConsoleWhisper.Tests {
	[TestClass]
	public class RuntimeReportTests {
		[TestMethod]
		public void UnloadedRuntimeIsReportedAsNotLoaded() {
			Assert.AreEqual("not loaded", WhisperHelper.FormatRuntimeName(null));
		}

		[TestMethod]
		public void CpuRuntimeIsReportedByName() {
			Assert.AreEqual("Cpu", WhisperHelper.FormatRuntimeName(RuntimeLibrary.Cpu));
		}

		[TestMethod]
		public void CudaRuntimeIsReportedByName() {
			Assert.AreEqual("Cuda", WhisperHelper.FormatRuntimeName(RuntimeLibrary.Cuda));
		}
	}
}
