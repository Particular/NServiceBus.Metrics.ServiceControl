﻿namespace NServiceBus.Metrics.ServiceControl
{
    using System;
    using System.IO;

    static class LongValueWriterV1
    {
        const long Version = 1;

        // WIRE FORMAT, Version: 1

        // 0                   1                   2                   3
        // 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        //+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        //|     Version   | Min date time | count | date1 |     value 1   |
        //+-------+-------+-------+-------+---------------+-------+-------+
        //| date2 |   value  2    | date3 |     value 3   | date4 | ...   |
        //+-------+---------------+-------+---------------+-------+-------+
        public static void Write(BinaryWriter outputWriter, ArraySegment<RingBuffer.Entry> chunk)
        {
            var array = chunk.Array;
            var offset = chunk.Offset;
            var count = chunk.Count;

            Array.Sort(array, offset, count, EntryTickComparer.Instance);

            var minDate = array[offset].Ticks;

            outputWriter.Write(Version);
            outputWriter.Write(minDate);
            outputWriter.Write(count);

            for (var i = 0; i < count; i++)
            {
                // int allows to write ticks of 7minutes, as reporter runs much more frequent, this can be int
                var date = (int)(array[offset + i].Ticks - minDate);
                outputWriter.Write(date);
                outputWriter.Write(array[offset + i].Value);
            }
        }
    }
}