﻿namespace ServiceControl.Monitoring.Messaging
{
    using System;
    using System.Collections.Concurrent;
    using NServiceBus;

    public abstract class RawMessage : IMessage
    {
        public int Length => Index;

        void Clear()
        {
            Index = InitialIndex;
            Entries.AsSpan(0, MaxEntries).Clear();
        }

        public readonly Entry[] Entries = new Entry[MaxEntries];

        protected int Index = InitialIndex;
        public const int MaxEntries = 512;
        protected const int InitialIndex = 0;

        public bool IsFull => Index == MaxEntries;

        public struct Entry
        {
            public long DateTicks;
            public long Value;
        }

        public class Pool<T> where T : RawMessage, new()
        {
            public T Lease()
            {
                if (pool.TryPop(out var value))
                {
                    return value;
                }

                return new T();
            }

            public void Release(T message)
            {
                message.Clear();
                pool.Push(message);
            }

            readonly ConcurrentStack<T> pool = new ConcurrentStack<T>();
            public static readonly Pool<T> Default = new Pool<T>();
        }
    }
}