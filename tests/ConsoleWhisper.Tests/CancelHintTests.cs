namespace ConsoleWhisper.Tests {
	[TestClass]
	public class CancelHintTests {
		[TestMethod]
		public async Task ForceQuitHintIsShownWhenCancellationDragsOn() {
			var shown = 0;

			await Runner.ShowForceQuitHint(CancellationToken.None, TimeSpan.FromMilliseconds(20), () => shown++);

			Assert.AreEqual(1, shown);
		}

		[TestMethod]
		public async Task ForceQuitHintIsSkippedWhenTheRunFinishesFirst() {
			using var hintCancellation = new CancellationTokenSource();
			var shown = 0;

			var hint = Runner.ShowForceQuitHint(hintCancellation.Token, TimeSpan.FromSeconds(30), () => shown++);
			hintCancellation.Cancel();
			await hint;

			Assert.AreEqual(0, shown);
		}
	}
}
