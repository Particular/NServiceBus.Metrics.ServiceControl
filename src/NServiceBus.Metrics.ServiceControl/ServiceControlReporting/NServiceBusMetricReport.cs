﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Logging;
using NServiceBus.Routing;
using NServiceBus.Transport;

namespace NServiceBus.Metrics.ServiceControl.ServiceControlReporting
{
    class NServiceBusMetricReport
    {
        public NServiceBusMetricReport(IDispatchMessages dispatcher, ReportingOptions options, Dictionary<string, string> headers, MetricsContext metricsContext)
        {
            this.dispatcher = dispatcher;
            this.headers = headers;
            this.metricsContext = metricsContext;

            destination = new UnicastAddressTag(options.ServiceControlMetricsAddress);
        }

        public async Task RunReportAsync()
        {
            var stringBody = $@"{{""Data"" : {metricsContext.ToJson()}}}";
            var body = Encoding.UTF8.GetBytes(stringBody);

            var message = new OutgoingMessage(Guid.NewGuid().ToString(), headers, body);
            var operation = new TransportOperation(message, destination);

            try
            {
                await dispatcher.Dispatch(new TransportOperations(operation), transportTransaction, new ContextBag())
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                log.Error($"Error while sending metric data to {destination}.", exception);
            }
        }

        UnicastAddressTag destination;
        IDispatchMessages dispatcher;
        Dictionary<string, string> headers;
        readonly MetricsContext metricsContext;
        TransportTransaction transportTransaction = new TransportTransaction();

        static ILog log = LogManager.GetLogger<NServiceBusMetricReport>();
    }
}