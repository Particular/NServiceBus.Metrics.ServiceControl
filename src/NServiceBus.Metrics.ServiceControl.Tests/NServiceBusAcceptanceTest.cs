namespace NServiceBus.Metrics.ServiceControl.Tests
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
    }
}