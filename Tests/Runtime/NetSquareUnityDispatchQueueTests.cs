using NetSquare.Core;
using NUnit.Framework;

namespace NetSquare.Client.Tests
{
    /// <summary>
    /// Verifies deterministic saturation behavior of the Unity callback queue.
    /// </summary>
    public sealed class NetSquareUnityDispatchQueueTests
    {
        /// <summary>
        /// Confirms RejectNewest never grows beyond capacity.
        /// </summary>
        [Test]
        public void RejectNewestKeepsStrictCapacity()
        {
            NetSquareUnityDispatchQueue queue =
                new NetSquareUnityDispatchQueue(
                    2,
                    NetSquareDispatchOverflowPolicy.RejectNewest);

            Assert.That(queue.TryEnqueue(DoNothing, null), Is.True);
            Assert.That(queue.TryEnqueue(DoNothing, null), Is.True);
            Assert.That(queue.TryEnqueue(DoNothing, null), Is.False);
            Assert.That(queue.Count, Is.EqualTo(2));
            Assert.That(queue.RejectedCount, Is.EqualTo(1));
        }

        /// <summary>
        /// Confirms DropOldest retains the newest callback at a fixed capacity.
        /// </summary>
        [Test]
        public void DropOldestRetainsNewestCallback()
        {
            NetSquareUnityDispatchQueue queue =
                new NetSquareUnityDispatchQueue(
                    1,
                    NetSquareDispatchOverflowPolicy.DropOldest);
            int observed = 0;

            queue.TryEnqueue(_ => observed = 1, null);
            queue.TryEnqueue(_ => observed = 2, null);
            queue.Drain(1);

            Assert.That(observed, Is.EqualTo(2));
            Assert.That(queue.Count, Is.Zero);
            Assert.That(queue.RejectedCount, Is.EqualTo(1));
        }

        /// <summary>
        /// Provides a no-op callback for queue capacity tests.
        /// </summary>
        /// <param name="message">Unused message.</param>
        private static void DoNothing(NetworkMessage message)
        {
        }
    }
}
