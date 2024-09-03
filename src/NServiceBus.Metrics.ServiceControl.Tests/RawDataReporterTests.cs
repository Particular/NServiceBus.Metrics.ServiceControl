namespace NServiceBus.Metrics.ServiceControl.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Metrics.ServiceControl;
    using NUnit.Framework;

    public class RawDataReporterTests
    {
        RingBuffer buffer;
        MockSender sender;

        [SetUp]
        public void SetUp()
        {
            buffer = new RingBuffer();
            sender = new MockSender();
        }

        [Test]
        public async Task When_flush_size_is_reached()
        {
            var reporter = new RawDataReporter(sender.ReportPayload, buffer, WriteEntriesValues, 4, TimeSpan.MaxValue);
            reporter.Start();
            buffer.TryWrite(1);
            buffer.TryWrite(2);
            buffer.TryWrite(3);
            buffer.TryWrite(4);

            AssertValues([1, 2, 3, 4]);

            await reporter.Stop();
        }

        [Test]
        public async Task When_flush_size_is_reached_only_maximum_number_is_flushed()
        {
            var reporter = new RawDataReporter(sender.ReportPayload, buffer, WriteEntriesValues, 4, TimeSpan.MaxValue);
            reporter.Start();
            buffer.TryWrite(1);
            buffer.TryWrite(2);
            buffer.TryWrite(3);
            buffer.TryWrite(4);

            AssertValues([1, 2], [3, 4]);

            await reporter.Stop();
        }

        [Test]
        public async Task When_flushing_should_use_parallel_sends()
        {
            // this test aims at simulating the consumers that are slow enough to fill parallelism of reporter
            // it asserts that no progress is done till one of them finishes
            const int max = RawDataReporter.MaxParallelConsumers;

            var payloads = new ConcurrentQueue<byte[]>();
            var tcs = new TaskCompletionSource<object>();
            var semaphore = new SemaphoreSlim(0, max);

            var counter = 0;

            Task Report(byte[] payload, CancellationToken cancellationToken)
            {
                var value = ReadValues(payload)[0];
                if (value < max)
                {
                    payloads.Enqueue(payload);
                    semaphore.Release();
                    return tcs.Task;
                }

                payloads.Enqueue(payload);
                Interlocked.Increment(ref counter);
                return Task.FromResult(0);
            }

            var reporter = new RawDataReporter(Report, buffer, WriteEntriesValues, flushSize: 1, maxFlushSize: 1, maxSpinningTime: TimeSpan.MaxValue);
            reporter.Start();

            for (var i = 0; i < max; i++)
            {
                buffer.TryWrite(i);
            }

            // additional write
            buffer.TryWrite(max);

            for (var i = 0; i < max; i++)
            {
                await semaphore.WaitAsync();
            }

            // no new reports scheduled before previous complete
            Assert.That(counter, Is.EqualTo(0));

            // complete all the reports
            tcs.SetResult("");

            await reporter.Stop();

            // should have run the additional one
            Assert.That(counter, Is.EqualTo(1));
            Assert.That(payloads.Count, Is.EqualTo(max + 1));
        }

        [Test]
        public async Task When_max_spinning_time_is_reached()
        {
            var maxSpinningTime = TimeSpan.FromMilliseconds(100);

            var reporter = new RawDataReporter(sender.ReportPayload, buffer, WriteEntriesValues, int.MaxValue, maxSpinningTime);
            reporter.Start();
            buffer.TryWrite(1);
            buffer.TryWrite(2);
            await Task.Delay(maxSpinningTime.Add(TimeSpan.FromMilliseconds(200)));

            AssertValues([1, 2]);

            await reporter.Stop();
        }

        [Test]
        public async Task When_stopped()
        {
            var reporter = new RawDataReporter(sender.ReportPayload, buffer, WriteEntriesValues, int.MaxValue, TimeSpan.MaxValue);
            reporter.Start();
            buffer.TryWrite(1);
            buffer.TryWrite(2);

            await reporter.Stop();

            AssertValues([1, 2]);
        }

        [Test]
        public async Task When_high_throughput_endpoint_shuts_down()
        {
            const int testSize = 10000;
            var queue = new ConcurrentQueue<byte[]>();

            Task Send(byte[] data, CancellationToken cancellationToken)
            {
                queue.Enqueue(data);
                return Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken); // delay to make it realistically long
            }

            var reporter = new RawDataReporter(Send, buffer, WriteEntriesValues);
            reporter.Start();

            // write values
            for (var i = 0; i < testSize; i++)
            {
                while (buffer.TryWrite(i) == false)
                {
                    // spin till it's written
                }
            }

            await reporter.Stop();

            var values = new HashSet<long>();
            foreach (var current in queue.ToArray().Select(ReadValues))
            {
                foreach (var v in current)
                {
                    values.Add(v);
                }
            }

            Assert.That(values.Count, Is.EqualTo(testSize));
        }

        void AssertValues(params long[][] values)
        {
            var bodies = sender.bodies.ToArray();
            var i = 0;
            foreach (var body in bodies)
            {
                var encodedValues = ReadValues(body);
                Assert.That(encodedValues, Is.EquivalentTo(values[i]));
            }
        }

        static List<long> ReadValues(byte[] body)
        {
            var encodedValues = new List<long>();
            var reader = new BinaryReader(new MemoryStream(body));
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                encodedValues.Add(reader.ReadInt64());
            }
            return encodedValues;
        }

        static void WriteEntriesValues(ArraySegment<RingBuffer.Entry> entries, BinaryWriter writer)
        {
            foreach (var entry in entries)
            {
                writer.Write(entry.Value);
            }
        }

        class MockSender
        {
            public List<byte[]> bodies = [];

#pragma warning disable IDE0060 // Remove unused parameter
            public Task ReportPayload(byte[] body, CancellationToken cancellationToken = default)
#pragma warning restore IDE0060 // Remove unused parameter
            {
                lock (bodies)
                {
                    bodies.Add(body);
                }

                return Task.FromResult(0);
            }
        }
    }
}
