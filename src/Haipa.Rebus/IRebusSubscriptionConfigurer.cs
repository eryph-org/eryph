using Rebus.Config;
using Rebus.Subscriptions;

namespace Haipa.Rebus
{
    public interface IRebusSubscriptionConfigurer
    {
        void Configure(StandardConfigurer<ISubscriptionStorage> timeoutManager);
    }
}