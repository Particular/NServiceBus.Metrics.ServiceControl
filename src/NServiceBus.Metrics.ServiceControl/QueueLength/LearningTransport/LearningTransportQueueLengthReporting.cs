namespace NServiceBus.Metrics.ServiceControl
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Features;
    using Settings;
    using Transport;

    class LearningTransportQueueLengthReporting : Feature
    {
        public LearningTransportQueueLengthReporting()
        {
            EnableByDefault();
            DependsOn<ReportQueueLengthMetric>();
            Prerequisite(ctx => ctx.Settings.Get<TransportDefinition>() is LearningTransport, "Learning Transport is not in use");
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask(b =>
            {
                var probe = b.Build<IQueueLengthProbe>();
                var queueLengthFinder = new LearningTransportQueueLengthFinder(probe);
                return new QueueLengthCalculator(context.Settings, queueLengthFinder);
            });
        }

        class QueueLengthCalculator : FeatureStartupTask
        {
            ReadOnlySettings settings;
            LearningTransportQueueLengthFinder queueLengthFinder;
            TimeSpan delayBetweenChecks = TimeSpan.FromMilliseconds(500);

            protected override Task OnStart(IMessageSession session)
            {
                task = Task.Run(async () =>
                {
                    await queueLengthFinder.Warmup(settings).ConfigureAwait(false);
                    while (!cancellationTokenSource.IsCancellationRequested)
                    {
                        await queueLengthFinder.UpdateQueueLength().ConfigureAwait(false);
                        await Task.Delay(delayBetweenChecks).ConfigureAwait(false);
                    }
                });

                return Task.FromResult(0);
            }

            protected override Task OnStop(IMessageSession session)
            {
                cancellationTokenSource.Cancel();

                return task;
            }

            readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            Task task;

            public QueueLengthCalculator(ReadOnlySettings settings, LearningTransportQueueLengthFinder queueLengthFinder)
            {
                this.settings = settings;
                this.queueLengthFinder = queueLengthFinder;
            }
        }
    }
}