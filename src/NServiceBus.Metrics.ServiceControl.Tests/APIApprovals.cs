namespace NServiceBus.Metrics.ServiceControl.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using ApprovalTests;
    using ApprovalTests.Core;
    using ApprovalTests.Writers;
    using NUnit.Framework;
    using PublicApiGenerator;

    [TestFixture]
    public class APIApprovals
    {
        static readonly Assembly Assembly = typeof(BusConfigurationExtensions).Assembly;

        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Approve()
        {
            var publicApi = Filter(ApiGenerator.GeneratePublicApi(Assembly));
            Approvals.Verify(WriterFactory.CreateTextWriter(publicApi, "cs"), GetNamer(), Approvals.GetReporter());
        }

        string Filter(string api)
        {
            var nl = Environment.NewLine;

            var lines = api.Split(new[] { nl }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.StartsWith("[assembly: System.Runtime.Versioning.TargetFrameworkAttribute"))
                .Where(line => !string.IsNullOrWhiteSpace(line));

            return string.Join(nl, lines);
        }


        static IApprovalNamer GetNamer([CallerFilePath] string path = "")
        {
            var dir = Path.GetDirectoryName(path);
            var name = Assembly.GetName().Name;

            return new Namer
            {
                Name = name,
                SourcePath = dir,
            };
        }

        class Namer : IApprovalNamer
        {
            public string SourcePath { get; set; }
            public string Name { get; set; }
        }
    }
}