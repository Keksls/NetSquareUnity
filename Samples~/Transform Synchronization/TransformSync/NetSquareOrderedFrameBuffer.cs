using System;

namespace NetSquare.Client
{
    /// <summary>
    /// Stores timestamped frames in a bounded circular array with allocation-free front removal.
    /// </summary>
    /// <typeparam name="T">Frame type.</typeparam>
    public sealed class NetSquareOrderedFrameBuffer<T>
    {
        #region Fields
        private readonly Func<T, float> timeSelector;
        private readonly Func<T, uint> sequenceSelector;
        private T[] items;
        private int head;
        private int count;
        #endregion

        #region Properties
        public int Count { get { return count; } }
        public int Capacity { get { return items.Length; } }

        public T this[int index]
        {
            get
            {
                ValidateIndex(index);
                return GetAt(index);
            }
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a bounded ordered frame buffer.
        /// </summary>
        /// <param name="capacity">Maximum retained frames.</param>
        /// <param name="timeSelector">Frame timestamp selector.</param>
        /// <param name="sequenceSelector">Frame sequence selector.</param>
        public NetSquareOrderedFrameBuffer(
            int capacity,
            Func<T, float> timeSelector,
            Func<T, uint> sequenceSelector)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            this.timeSelector = timeSelector ?? throw new ArgumentNullException(nameof(timeSelector));
            this.sequenceSelector =
                sequenceSelector ?? throw new ArgumentNullException(nameof(sequenceSelector));
            items = new T[capacity];
        }
        #endregion

        #region Mutation
        /// <summary>
        /// Inserts or replaces one frame while preserving timestamp order and the capacity bound.
        /// </summary>
        /// <param name="item">Frame to retain.</param>
        /// <returns>True when the frame was retained.</returns>
        public bool AddOrReplace(T item)
        {
            uint sequence = sequenceSelector(item);
            float time = timeSelector(item);
            for (int i = 0; i < count; i++)
            {
                T existing = GetAt(i);
                if ((sequence != 0u && sequenceSelector(existing) == sequence) ||
                    timeSelector(existing) == time)
                {
                    SetAt(i, item);
                    Reposition(i);
                    return true;
                }
            }

            int insertionIndex = FindInsertionIndex(time);
            if (count == items.Length)
            {
                // A frame older than every retained frame has no interpolation value under pressure.
                if (insertionIndex == 0)
                    return false;

                RemoveFirst();
                insertionIndex--;
            }

            for (int i = count; i > insertionIndex; i--)
                SetAt(i, GetAt(i - 1));
            SetAt(insertionIndex, item);
            count++;
            return true;
        }

        /// <summary>
        /// Removes the oldest frame in constant time.
        /// </summary>
        /// <returns>True when a frame was removed.</returns>
        public bool RemoveFirst()
        {
            if (count == 0)
                return false;

            items[head] = default(T);
            head = (head + 1) % items.Length;
            count--;
            if (count == 0)
                head = 0;
            return true;
        }

        /// <summary>
        /// Changes the capacity while retaining the newest frames.
        /// </summary>
        /// <param name="capacity">New positive capacity.</param>
        public void SetCapacity(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (capacity == items.Length)
                return;

            T[] resized = new T[capacity];
            int retainedCount = Math.Min(count, capacity);
            int sourceStart = count - retainedCount;
            for (int i = 0; i < retainedCount; i++)
                resized[i] = GetAt(sourceStart + i);

            items = resized;
            head = 0;
            count = retainedCount;
        }

        /// <summary>
        /// Removes every retained frame.
        /// </summary>
        public void Clear()
        {
            Array.Clear(items, 0, items.Length);
            head = 0;
            count = 0;
        }
        #endregion

        #region Ordering
        /// <summary>
        /// Finds the logical insertion position for one timestamp.
        /// </summary>
        /// <param name="time">Frame timestamp.</param>
        /// <returns>Logical insertion index.</returns>
        private int FindInsertionIndex(float time)
        {
            int low = 0;
            int high = count;
            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                if (timeSelector(GetAt(middle)) <= time)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }

        /// <summary>
        /// Restores order after replacing an existing frame.
        /// </summary>
        /// <param name="index">Logical index of the replacement.</param>
        private void Reposition(int index)
        {
            while (index > 0 &&
                   timeSelector(GetAt(index)) < timeSelector(GetAt(index - 1)))
            {
                Swap(index, index - 1);
                index--;
            }

            while (index < count - 1 &&
                   timeSelector(GetAt(index)) > timeSelector(GetAt(index + 1)))
            {
                Swap(index, index + 1);
                index++;
            }
        }

        /// <summary>
        /// Swaps two logical items.
        /// </summary>
        /// <param name="left">First logical index.</param>
        /// <param name="right">Second logical index.</param>
        private void Swap(int left, int right)
        {
            T value = GetAt(left);
            SetAt(left, GetAt(right));
            SetAt(right, value);
        }
        #endregion

        #region Indexing
        /// <summary>
        /// Reads one logical item without bounds validation.
        /// </summary>
        /// <param name="index">Logical index.</param>
        /// <returns>Stored item.</returns>
        private T GetAt(int index)
        {
            return items[(head + index) % items.Length];
        }

        /// <summary>
        /// Writes one logical item, including the next free tail slot.
        /// </summary>
        /// <param name="index">Logical index.</param>
        /// <param name="value">Item to store.</param>
        private void SetAt(int index, T value)
        {
            items[(head + index) % items.Length] = value;
        }

        /// <summary>
        /// Validates one externally requested logical index.
        /// </summary>
        /// <param name="index">Logical index.</param>
        private void ValidateIndex(int index)
        {
            if (index < 0 || index >= count)
                throw new ArgumentOutOfRangeException(nameof(index));
        }
        #endregion
    }
}
