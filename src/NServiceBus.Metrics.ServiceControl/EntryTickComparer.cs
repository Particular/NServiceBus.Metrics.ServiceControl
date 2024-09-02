namespace NServiceBus.Metrics.ServiceControl
{
    using System.Collections.Generic;

    sealed class EntryTickComparer : IComparer<RingBuffer.Entry>
    {
        public static readonly EntryTickComparer Instance = new EntryTickComparer();

        EntryTickComparer() { }

        public int Compare(RingBuffer.Entry x, RingBuffer.Entry y) => x.Ticks.CompareTo(y.Ticks);
    }
}