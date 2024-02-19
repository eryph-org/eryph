using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Ardalis.Specification;
using Eryph.ConfigModel.Networks;
using Eryph.Core.Network;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using MartinCostello.Logging.XUnit;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.Networks
{
    public class NetworkConfigRealizerTests
    {
        private readonly ITestOutputHelper _testOutput;

        public NetworkConfigRealizerTests(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        [Fact]
        public async Task Creates_new_network()
        {
            var logger = new XUnitLogger("log", _testOutput, new XUnitLoggerOptions());

            var stateStore = new Mock<IStateStore>();
            var projectId = new Guid();
            var networkConfig = new ProjectNetworksConfig()
            {
                Networks = new []
                {
                    new NetworkConfig
                    {
                        Name = "test",
                        
                    }
                }
            };

            var networkProviderConfig = new NetworkProvidersConfiguration()
            {
                NetworkProviders = new NetworkProvider[]
                {
                    new NetworkProvider
                    {

                    }
                }
            };

            var realizer = new NetworkConfigRealizer(stateStore.Object, logger);
            await realizer.UpdateNetwork(projectId, networkConfig, networkProviderConfig);


        }
    }

}
