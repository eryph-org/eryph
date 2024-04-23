using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.VmManagement.Converging;
using LanguageExt;
using LanguageExt.Common;
using Xunit;

namespace Eryph.VmManagement.Test
{
    public class ConvergeNestedVirtualizationTests : IClassFixture<ConvergeFixture>
    {
        private readonly ConvergeFixture _fixture;

        public ConvergeNestedVirtualizationTests(ConvergeFixture fixture)
        {
            _fixture = fixture;
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(true, null)]
        [InlineData(false, null)]

        public async Task Converges_NestedVirtualization_if_necessary(bool exposed, bool? shouldExpose)
        {
            _fixture.Reset();
            var vmData = _fixture.Engine.ToPsObject(
                new Data.Full.VirtualMachineInfo());
            var called = false;


            if(shouldExpose.HasValue)
                _fixture.Config.Capabilities = new[]
                {
                    new CatletCapabilityConfig
                    {
                        Name = EryphConstants.Capabilities.NestedVirtualization,
                        Details = new []{shouldExpose.GetValueOrDefault() ? "on" : "off"}
                    }
                };

            AssertCommand? runCommand = null;
            _fixture.Engine.RunCallback = command =>
            {
                called = true;
                runCommand = command;
                return Unit.Default;
            };
            _fixture.Engine.GetValuesCallback = (_, command) =>
            {
                if (command.ToString().StartsWith("get-VMProcessor"))
                {
                    return new object []{exposed}.ToSeq();
                }

                return new PowershellFailure{ Message = $"Unexpected command {command}"};
            };


            var convergeTask = new ConvergeNestedVirtualization(_fixture.Context);
            await convergeTask.Converge(vmData);

            if (exposed == shouldExpose || shouldExpose == null)
            {
                Assert.False(called);
                return;
            }
            
            Assert.True(called);
            Assert.NotNull(runCommand);
            runCommand.ShouldBeCommand("Set-VMProcessor")
                .ShouldBeParam("VM")
                .ShouldBeParam("ExposeVirtualizationExtensions", shouldExpose);


        }

    }
}
