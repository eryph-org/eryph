using Rebus.Config;
using Rebus.Persistence.InMem;
using Rebus.Timeouts;

namespace Eryph.Rebus
{
    public class InMemoryTimeoutConfigurer : IRebusTimeoutConfigurer
    {
        public void Configure(StandardConfigurer<ITimeoutManager> timeoutManager)
        {
            timeoutManager.StoreInMemory();
        }
    }
}