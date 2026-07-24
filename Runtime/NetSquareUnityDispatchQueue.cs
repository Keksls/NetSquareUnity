using NetSquare.Core;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace NetSquare.Client
{
    /// <summary>
    /// Defines how the bounded Unity callback queue reacts to saturation.
    /// </summary>
    public enum NetSquareDispatchOverflowPolicy
    {
        RejectNewest = 0,
        DropOldest = 1
    }

    /// <summary>
    /// Transfers network callbacks to Unity's main thread without unbounded memory growth.
    /// </summary>
    public sealed class NetSquareUnityDispatchQueue
    {
        #region Fields
        private readonly ConcurrentQueue<NetSquareActionData> queue;
        private readonly int capacity;
        private readonly NetSquareDispatchOverflowPolicy overflowPolicy;
        private int count;
        private long rejectedCount;
        #endregion

        #region Properties
        public int Count
        {
            get { return Volatile.Read(ref count); }
        }

        public long RejectedCount
        {
            get { return Interlocked.Read(ref rejectedCount); }
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a bounded main-thread callback queue.
        /// </summary>
        /// <param name="capacity">Maximum callbacks retained at once.</param>
        /// <param name="overflowPolicy">Policy applied when the queue is full.</param>
        public NetSquareUnityDispatchQueue(
            int capacity,
            NetSquareDispatchOverflowPolicy overflowPolicy = NetSquareDispatchOverflowPolicy.RejectNewest)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            this.capacity = capacity;
            this.overflowPolicy = overflowPolicy;
            queue = new ConcurrentQueue<NetSquareActionData>();
        }
        #endregion

        #region Enqueue
        /// <summary>
        /// Attempts to retain a callback for execution on the Unity main thread.
        /// </summary>
        /// <param name="action">Action to execute.</param>
        /// <param name="message">Message supplied to the action.</param>
        /// <returns>True when the callback was retained.</returns>
        public bool TryEnqueue(NetSquareAction action, NetworkMessage message)
        {
            // Reserve capacity before publishing the item so concurrent producers cannot exceed the bound.
            int reservedCount = Interlocked.Increment(ref count);
            if (reservedCount <= capacity)
            {
                queue.Enqueue(new NetSquareActionData(action, message));
                return true;
            }

            Interlocked.Decrement(ref count);
            if (overflowPolicy == NetSquareDispatchOverflowPolicy.DropOldest &&
                TryDropOldest())
            {
                return TryEnqueue(action, message);
            }

            Interlocked.Increment(ref rejectedCount);
            return false;
        }

        /// <summary>
        /// Removes the oldest queued callback to make room for fresher work.
        /// </summary>
        /// <returns>True when one callback was removed.</returns>
        private bool TryDropOldest()
        {
            if (!queue.TryDequeue(out _))
                return false;

            Interlocked.Decrement(ref count);
            Interlocked.Increment(ref rejectedCount);
            return true;
        }
        #endregion

        #region Consumption
        /// <summary>
        /// Executes up to the requested number of queued callbacks.
        /// </summary>
        /// <param name="maximumItems">Maximum callbacks executed by this call.</param>
        /// <returns>Number of callbacks executed.</returns>
        public int Drain(int maximumItems)
        {
            if (maximumItems <= 0)
                return 0;

            int processed = 0;
            while (processed < maximumItems && queue.TryDequeue(out NetSquareActionData item))
            {
                Interlocked.Decrement(ref count);
                processed++;
                item.Invoke();
            }

            return processed;
        }

        /// <summary>
        /// Removes every pending callback without executing it.
        /// </summary>
        public void Clear()
        {
            while (queue.TryDequeue(out _))
                Interlocked.Decrement(ref count);
        }
        #endregion
    }
}
