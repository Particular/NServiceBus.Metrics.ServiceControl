namespace NServiceBus.Metrics.ServiceControl
{
    using System.Threading.Tasks;
    using Features;
    using global::ServiceControl.Monitoring.Data;
    using Hosting;

    class ReportQueueLengthMetric : Feature
    {
        public ReportQueueLengthMetric()
        {
            EnableByDefault();
            DependsOn<ReportingFeature>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var buffer = new RingBuffer();
            var writer = new TaggedLongValueWriterV1();

            // NOTE: Exposes a hook for transports to write queue length to
            context.Container.RegisterSingleton<IQueueLengthProbe>(new BufferedQueueLengthProbe(buffer, writer));

            SetupServiceControlReporting(context, buffer, writer);
        }

        void SetupServiceControlReporting(FeatureConfigurationContext context, RingBuffer buffer, TaggedLongValueWriterV1 writer)
        {
            var metricsOptions = context.Settings.Get<MetricsOptions>();
            var reportingOptions = ReportingOptions.Get(metricsOptions);
            var baseHeaderFactory = new MetricsReportBaseHeaderFactory(context.Settings.EndpointName(), reportingOptions);

            context.RegisterStartupTask(builder =>
            {
                var hostInformation = builder.Build<HostInformation>();

                var headers = baseHeaderFactory.BuildBaseHeaders(hostInformation);

                var rawDataReporterFactory = new RawDataReporterFactory(builder, reportingOptions, headers);

                var queueLengthReporter = rawDataReporterFactory.CreateReporter("QueueLength", buffer, writer);

                return new SendDataToServiceControl(queueLengthReporter);
            });
        }

        class SendDataToServiceControl : FeatureStartupTask
        {
            public SendDataToServiceControl(RawDataReporter reporter)
            {
                this.reporter = reporter;
            }

            protected override Task OnStart(IMessageSession session)
            {
                reporter.Start();
                return Task.FromResult(0);
            }

            protected override async Task OnStop(IMessageSession session)
            {
                await reporter.Stop().ConfigureAwait(false);

                reporter.Dispose();
            }

            RawDataReporter reporter;
        }
    }
}