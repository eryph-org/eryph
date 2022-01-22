using Rebus.Config;
using Rebus.Subscriptions;

namespace Eryph.Rebus
{
    public interface IRebusSubscriptionConfigurer
    {
        void Configure(StandardConfigurer<ISubscriptionStorage> timeoutManager);
    }
}