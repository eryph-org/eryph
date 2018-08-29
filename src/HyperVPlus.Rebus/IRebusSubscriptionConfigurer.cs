using Rebus.Config;
using Rebus.Subscriptions;

namespace HyperVPlus.Rebus
{
    public interface IRebusSubscriptionConfigurer
    {
        void Configure(StandardConfigurer<ISubscriptionStorage> timeoutManager);
    }
}