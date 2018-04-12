namespace NServiceBus.Metrics.ServiceControl
{
    using System.Collections.Generic;
    using Hosting;
    using Support;

    class MetricsReportBaseHeaderFactory
    {
        public MetricsReportBaseHeaderFactory(string endpointName, ReportingOptions options)
        {
            this.endpointName = endpointName;
            this.options = options;
        }

        internal Dictionary<string, string> BuildBaseHeaders(HostInformation hostInformation)
        {
            var headers = new Dictionary<string, string>
            {
                {Headers.OriginatingEndpoint, endpointName},
                {Headers.OriginatingMachine, RuntimeEnvironment.MachineName},
                {Headers.OriginatingHostId, hostInformation.HostId.ToString("N")},
                {Headers.HostDisplayName, hostInformation.DisplayName},
            };

            if (options.TryGetValidEndpointInstanceIdOverride(out var instanceId))
            {
                headers.Add(MetricHeaders.MetricInstanceId, instanceId);
            }

            return headers;
        }

        readonly string endpointName;
        readonly ReportingOptions options;
    }
}