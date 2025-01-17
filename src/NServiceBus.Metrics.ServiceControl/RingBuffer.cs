﻿namespace NServiceBus.Metrics.ServiceControl
{
    using System;
    using System.Threading;

    sealed class RingBuffer
    {
        internal const int Size = 4096;
        const long SizeMask = Size - 1;
        const long EpochMask = ~SizeMask;

        long nextToWrite;
        long nextToConsume;

        Entry[] entries = new Entry[Size];

        public struct Entry(long ticks, long value, int tag = 0)
        {
            public long Ticks = ticks;
            public long Value = value;
            public int Tag = tag;
        }

        public bool TryWrite(long value, int tag = 0)
        {
            var readNextWrite = Volatile.Read(ref nextToWrite);
            long index;

            do
            {
                var consume = Volatile.Read(ref nextToConsume);

                // if writers overlaps with not consumed writes, end
                if (readNextWrite - consume >= Size)
                {
                    return false;
                }
                index = readNextWrite;

                // try to swap nextToWrite
                readNextWrite = Interlocked.CompareExchange(ref nextToWrite, index + 1, index);

                // do until the swap not succeeded
            }
            while (index != readNextWrite);

            // index is claimed, writing data
            var i = index & SizeMask;
            var ticks = DateTime.UtcNow.Ticks;

            entries[i].Value = value;
            entries[i].Tag = tag;

            Volatile.Write(ref entries[i].Ticks, ticks);

            return true;
        }

        internal long RoughlyEstimateItemsToConsume() => Volatile.Read(ref nextToWrite) - Volatile.Read(ref nextToConsume);

        // Consumes a chunk of entries. This method will call onChunk zero, or one time. No multiple calls will be issued.
        internal int Consume(int maxFlushSize, Action<ArraySegment<Entry>> onChunk)
        {
            var consume = Interlocked.CompareExchange(ref nextToConsume, 0, 0);
            var max = Volatile.Read(ref nextToWrite);

            var i = consume;

            // The epoch identifies the id of the current passage over the circular buffer.
            // This is used to ensure, that once the Consume goes over the edge of the buffer,
            // and starts from the beginning, this part won't be included in the result passed to onChunk.
            // If it was, this could not be represented as a continuous ArraySegment<Entry>.
            //
            // Example:
            // To consume a buffer with following values [5, 6, null, 4] the consumer would first consume
            // a segment [4] followed by consumption of [5, 6] in the next Consume call.
            var epoch = i & EpochMask;
            var length = 0;
            while (Volatile.Read(ref entries[i & SizeMask].Ticks) > 0 && i < max && length < maxFlushSize)
            {
                if ((i & EpochMask) != epoch)
                {
                    break;
                }

                length++;
                i++;
            }

            // [consume, i) - entries to process
            var indexStart = (int)(consume & SizeMask);

            if (length == 0)
            {
                return 0;
            }

            onChunk(new ArraySegment<Entry>(entries, indexStart, length));
            entries.AsSpan(indexStart, length).Clear();

            Interlocked.Add(ref nextToConsume, length);
            return length;
        }
    }
}
