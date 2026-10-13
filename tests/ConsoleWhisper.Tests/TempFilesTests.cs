using ConsoleWhisper.Module;

namespace ConsoleWhisper.Tests {
	[TestClass]
	public class TempFilesTests {
		[TestMethod]
		public void CleanupDeletesRegisteredFiles() {
			var first = Path.Combine(Path.GetTempPath(), $"cw-tempfiles-{Guid.NewGuid():N}.tmp");
			var second = Path.Combine(Path.GetTempPath(), $"cw-tempfiles-{Guid.NewGuid():N}.tmp");
			File.WriteAllText(first, "x");
			File.WriteAllText(second, "x");

			TempFiles.Register(first);
			TempFiles.Register(second);
			TempFiles.Cleanup();

			Assert.IsFalse(File.Exists(first));
			Assert.IsFalse(File.Exists(second));
		}

		[TestMethod]
		public void CleanupIgnoresRegisteredFilesThatNoLongerExist() {
			var missing = Path.Combine(Path.GetTempPath(), $"cw-tempfiles-{Guid.NewGuid():N}.tmp");

			TempFiles.Register(missing);
			TempFiles.Cleanup();

			Assert.IsFalse(File.Exists(missing));
		}
	}
}
