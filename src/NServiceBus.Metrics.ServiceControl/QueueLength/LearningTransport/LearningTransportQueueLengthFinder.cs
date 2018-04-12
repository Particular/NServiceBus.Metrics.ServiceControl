namespace NServiceBus.Metrics.ServiceControl
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Logging;
    using Settings;
    using Transport;
    
    class LearningTransportQueueLengthFinder 
    {
        public Task Warmup(ReadOnlySettings settings)
        {
            var transportFolder = GetTransportFolder(settings);
            var queueBindings = settings.Get<QueueBindings>();
            foreach (var q in queueBindings.ReceivingAddresses)
            {
                queues.Add(Tuple.Create(q, new DirectoryInfo(Path.Combine(transportFolder, q))));
            }
            return Task.FromResult(0);
        }

        #region Transport Implementation Details

        const string StorageLocationKey = "LearningTransport.StoragePath";

        static string GetTransportFolder(ReadOnlySettings settings)
        {
            if (!settings.TryGet(StorageLocationKey, out string storagePath))
            {
                var solutionRoot = FindSolutionRoot();
                storagePath = Path.Combine(solutionRoot, ".learningtransport");
            }
            return storagePath;
        }

        static string FindSolutionRoot()
        {
            var directory = AppDomain.CurrentDomain.BaseDirectory;

            while (true)
            {
                if (Directory.EnumerateFiles(directory).Any(file => file.EndsWith(".sln")))
                {
                    return directory;
                }

                var parent = Directory.GetParent(directory);

                if (parent == null)
                {
                    // throw for now. if we discover there is an edge then we can fix it in a patch.
                    throw new Exception("Couldn't find the solution directory for the learning transport. If the endpoint is outside the solution folder structure, make sure to specify a storage directory using the 'EndpointConfiguration.UseTransport<LearningTransport>().StorageDirectory()' API.");
                }

                directory = parent.FullName;
            }
        }

        #endregion

        public Task UpdateQueueLength()
        {
            foreach (var queue in queues)
            {
                try
                {
                    if (queue.Item2.Exists)
                    {
                        var msgCount = queue.Item2.EnumerateFiles().Count();
                        probe.Signal(queue.Item1, msgCount);
                    }
                }
                catch (DirectoryNotFoundException dnfex)
                {
                    Log.Error($"Error reading length of queue {queue.Item1}", dnfex);
                }
            }

            return Task.FromResult(0);
        }

        List<Tuple<string, DirectoryInfo>> queues = new List<Tuple<string, DirectoryInfo>>();
        IQueueLengthProbe probe;

        public LearningTransportQueueLengthFinder(IQueueLengthProbe probe)
        {
            this.probe = probe;
        }

        static ILog Log = LogManager.GetLogger<LearningTransportQueueLengthFinder>();
    }
}
