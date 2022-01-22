using Rebus.Config;
using Rebus.Timeouts;

namespace Eryph.Rebus
{
    public interface IRebusTimeoutConfigurer
    {
        void Configure(StandardConfigurer<ITimeoutManager> timeoutManager);
    }
}