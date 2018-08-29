using Rebus.Config;
using Rebus.Timeouts;

namespace HyperVPlus.Rebus
{
    public interface IRebusTimeoutConfigurer
    {
        void Configure(StandardConfigurer<ITimeoutManager> timeoutManager);
    }
}