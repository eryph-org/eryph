using Eryph.ConfigModel.Catlets;
using Eryph.VmManagement.Converging;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Eryph.VmManagement.Test
{
    public class ConvergeCpuTests : IClassFixture<ConvergeFixture>
    {
        private readonly ConvergeFixture _fixture;

        public ConvergeCpuTests(ConvergeFixture fixture)
        {
            _fixture = fixture;
        }

        [Theory]
        [InlineData(null, 1L)]
        [InlineData(1, 1L)]
        [InlineData(2, 1L)]
        public async Task Converges_Cpu_if_necessary(int? configCpu, long vmCpu)
        {

            _fixture.Config.Cpu = new CatletCpuConfig { Count = configCpu };
            var vmData = _fixture.Engine.ToPsObject(new Data.Full.VirtualMachineInfo { ProcessorCount = vmCpu });
            var called = false;

            _fixture.Engine.RunCallback = command =>
            {
                called = true;
                return Unit.Default;
            };

            var convergeTask = new ConvergeCPU(_fixture.Context);
            await convergeTask.Converge(vmData);

            if(configCpu.GetValueOrDefault(1) == vmCpu)
                Assert.False(called);
            else
                Assert.True(called);


        }

    }
}
