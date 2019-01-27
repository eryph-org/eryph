using Rebus.Config;
using Rebus.Timeouts;

namespace Haipa.Rebus
{
    public interface IRebusTimeoutConfigurer
    {
        void Configure(StandardConfigurer<ITimeoutManager> timeoutManager);
    }
}