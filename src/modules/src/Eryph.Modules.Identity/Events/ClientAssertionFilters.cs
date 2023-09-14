using System;
using System.Threading.Tasks;
using OpenIddict.Server;

namespace Eryph.Modules.Identity.Events;

public static class ClientAssertionFilters
{
    public sealed class RequireClientAssertion : IOpenIddictServerHandlerFilter<OpenIddictServerEvents.BaseContext>
    {
        /// <inheritdoc/>
        public ValueTask<bool> IsActiveAsync(OpenIddictServerEvents.BaseContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return new ValueTask<bool>(!string.IsNullOrEmpty(context.Transaction.Request?.ClientAssertion));
        }
    }

    public sealed class RequireNoClientAssertion : IOpenIddictServerHandlerFilter<OpenIddictServerEvents.BaseContext>
    {
        /// <inheritdoc/>
        public ValueTask<bool> IsActiveAsync(OpenIddictServerEvents.BaseContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return new ValueTask<bool>(string.IsNullOrEmpty(context.Transaction.Request?.ClientAssertion));
        }
    }
}