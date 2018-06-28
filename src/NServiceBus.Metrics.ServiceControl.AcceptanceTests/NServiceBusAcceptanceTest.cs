namespace NServiceBus.Metrics.ServiceControl.AcceptanceTests
{
    using System.Linq;
    using NUnit.Framework;

    [TestFixture]
    // ReSharper disable once PartialTypeWithSinglePart
    public abstract partial class NServiceBusAcceptanceTest
    {
        [SetUp]
        public void SetUp()
        {
            AcceptanceTesting.Customization.Conventions.EndpointNamingConvention = t =>
            {
                var classAndEndpoint = t.FullName.Split('.').Last();

                var testName = classAndEndpoint.Split('+').First();

                testName = testName.Replace("When_", "");

                var endpointBuilder = classAndEndpoint.Split('+').Last();


                testName = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(testName);

                testName = testName.Replace("_", "");



                return testName + "." + endpointBuilder;
            };

            AcceptanceTesting.Customization.Conventions.DefaultRunDescriptor = () => ScenarioDescriptors.Transports.Default;
        }

        protected static bool ContainsPattern(byte[] source, byte[] pattern)
        {
            return Enumerable.Range(0, source.Length - 1).Any(i => source.Skip(i).Take(pattern.Length).SequenceEqual(pattern));
        }
    }
}