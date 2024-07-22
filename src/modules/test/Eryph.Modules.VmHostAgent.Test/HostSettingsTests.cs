using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.VmHostAgent.Test;

public class HostSettingsTests
{
    [Fact]
    public async Task Test()
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        for (int i = 0; i < 100; i++)
        {
            var hostSettingsProvider = new HostSettingsProvider();

            var hostSettings = await hostSettingsProvider.GetHostSettings();
        }

        var elapsed = stopWatch.ElapsedMilliseconds;
        Assert.True(elapsed < 1000, $"Elapsed time {elapsed}ms");
    }
}