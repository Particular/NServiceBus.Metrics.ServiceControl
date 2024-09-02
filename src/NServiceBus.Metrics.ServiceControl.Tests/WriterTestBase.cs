namespace NServiceBus.Metrics.ServiceControl.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using NServiceBus.Metrics.ServiceControl;
    using NUnit.Framework;

    public abstract class WriterTestBase
    {
        MemoryStream ms;
        BinaryWriter bw;
        Action<BinaryWriter, ArraySegment<RingBuffer.Entry>> writer;

        internal WriterTestBase()
        {
            ms = new MemoryStream();
            bw = new BinaryWriter(ms);
        }

        internal void SetWriter(Action<BinaryWriter, ArraySegment<RingBuffer.Entry>> writer) => this.writer = writer;

        [SetUp]
        public void SetUp()
        {
            bw.Flush();
            ms.SetLength(0);
        }

        internal void Write(params RingBuffer.Entry[] entries)
        {
            // put additional values in front and at the end to make it a real segment
            var list = entries.ToList();
            list.Insert(0, new RingBuffer.Entry());
            list.Add(new RingBuffer.Entry());

            writer(bw, new ArraySegment<RingBuffer.Entry>([.. list], 1, entries.Length));

            bw.Flush();
        }

        protected void Assert(Action<BinaryWriter> write)
        {
            using var stream = new MemoryStream();
            using (var binaryWriter = new BinaryWriter(stream))
            {
                write(binaryWriter);
                binaryWriter.Flush();
            }

            NUnit.Framework.Assert.That(stream.ToArray(), Is.EquivalentTo(ms.ToArray()));
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            bw?.Dispose();
            ms?.Dispose();
        }
    }
}