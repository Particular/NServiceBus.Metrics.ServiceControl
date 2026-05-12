#nullable enable

namespace NServiceBus.Metrics.ServiceControl.Tests;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ServiceControlReporting;
using Transport;
using NUnit.Framework;

[TestFixture]
class MetadataReportTests
{
    const string DestinationAddress = "ServiceControl.Monitoring";
    static readonly TimeSpan TTBR = TimeSpan.FromHours(2);

    [Test]
    public async Task Should_dispatch_with_DiscardIfNotReceivedBefore()
    {
        var operations = await CaptureOperations([], CancellationToken.None);

        Assert.That(operations, Has.Count.EqualTo(1));
        Assert.That(
            operations[0].Properties.DiscardIfNotReceivedBefore?.MaxTime,
            Is.EqualTo(TTBR),
            "Should set DiscardIfNotReceivedBefore to the configured TTBR");
    }

    [Test]
    public async Task Should_dispatch_to_configured_destination()
    {
        var operations = await CaptureOperations([], CancellationToken.None);

        Assert.That(operations, Has.Count.EqualTo(1));
        Assert.That(operations[0].Destination, Is.EqualTo(DestinationAddress));
    }

    [Test]
    public async Task Should_pass_configured_headers()
    {
        var headers = new Dictionary<string, string>
        {
            { "H1", "V1" },
            { "H2", "V2" }
        };

        var operations = await CaptureOperations(headers, CancellationToken.None);

        Assert.That(operations, Has.Count.EqualTo(1));
        Assert.That(operations[0].Message.Headers, Does.ContainKey("H1").WithValue("V1"));
        Assert.That(operations[0].Message.Headers, Does.ContainKey("H2").WithValue("V2"));
    }

    [Test]
    public async Task Should_dispatch_serialized_endpoint_metadata_as_body()
    {
        var operations = await CaptureOperations([], CancellationToken.None);

        Assert.That(operations, Has.Count.EqualTo(1));
        var body = Encoding.UTF8.GetString(operations[0].Message.Body.Span);
        Assert.That(body, Does.Contain("\"PluginVersion\":3"), "Body should contain serialized endpoint metadata");
    }

    [Test]
    public async Task Should_not_throw_when_dispatch_fails()
    {
        var report = CreateMetaDataReport(dispatcher: new FailingDispatcher());

        await Assert.ThatAsync(() => report.RunReportAsync(CancellationToken.None), Throws.Nothing);
    }

    [Test]
    public async Task Should_propagate_cancellation_from_dispatcher()
    {
        var cancelledToken = new CancellationToken(true);
        var dispatcher = new CancelingDispatcher();
        var report = CreateMetaDataReport(dispatcher: dispatcher);

        await Assert.ThatAsync(() => report.RunReportAsync(cancelledToken), Throws.TypeOf<TaskCanceledException>());
    }

    static async Task<List<UnicastTransportOperation>> CaptureOperations(Dictionary<string, string> headers, CancellationToken cancellationToken)
    {
        var spy = new DispatchSpy();
        var report = CreateMetaDataReport(dispatcher: spy, headers: headers);
        await report.RunReportAsync(cancellationToken);
        return spy.Operations;
    }

    static MetadataReport CreateMetaDataReport(
        IMessageDispatcher? dispatcher = null,
        Dictionary<string, string>? headers = null)
    {
        dispatcher ??= new DispatchSpy();
        headers ??= [];

        var options = new ReportingOptions
        {
            ServiceControlMetricsAddress = DestinationAddress,
            TimeToBeReceived = TTBR
        };
        var endpointMetadata = new EndpointMetadata("MyEndpoint");

        return new MetadataReport(dispatcher, options, headers, endpointMetadata);
    }

    class DispatchSpy : IMessageDispatcher
    {
        public List<UnicastTransportOperation> Operations { get; } = [];

        public Task Dispatch(TransportOperations operations, TransportTransaction transportTransaction, CancellationToken cancellationToken = default)
        {
            Operations.AddRange(operations.UnicastTransportOperations);
            return Task.CompletedTask;
        }
    }

    class FailingDispatcher : IMessageDispatcher
    {
        public Task Dispatch(TransportOperations operations, TransportTransaction transportTransaction, CancellationToken cancellationToken = default)
            => throw new Exception("Dispatch failed");
    }

    class CancelingDispatcher : IMessageDispatcher
    {
        public Task Dispatch(TransportOperations operations, TransportTransaction transportTransaction, CancellationToken cancellationToken = default)
            => Task.FromCanceled(cancellationToken);
    }
}