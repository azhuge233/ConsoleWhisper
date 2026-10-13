using System;

namespace ConsoleWhisper.Tests {
	/// <summary>
	/// Gives every test its own directory under the system temp folder and removes it afterwards.
	/// </summary>
	public abstract class TestDirectoryBase {
		protected string TestRoot { get; private set; } = string.Empty;

		[TestInitialize]
		public void CreateTestDirectory() {
			TestRoot = Path.Combine(Path.GetTempPath(), $"cw-tests-{Guid.NewGuid():N}");
			Directory.CreateDirectory(TestRoot);
		}

		[TestCleanup]
		public void DeleteTestDirectory() {
			OnCleanup();

			if (Directory.Exists(TestRoot))
				Directory.Delete(TestRoot, true);
		}

		protected virtual void OnCleanup() { }

		protected string CreateFile(string relativePath) {
			var fullPath = Path.Combine(TestRoot, relativePath);
			Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
			File.WriteAllText(fullPath, "test");

			return fullPath;
		}
	}
}
