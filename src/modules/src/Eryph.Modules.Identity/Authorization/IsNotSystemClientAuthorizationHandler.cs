using System;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;

namespace Eryph.Modules.Identity.Authorization;

internal class IsNotSystemClientAuthorizationHandler
    : AuthorizationHandler<IsNotSystemClientRequirement, ClientApplicationDescriptor>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        IsNotSystemClientRequirement requirement,
        ClientApplicationDescriptor resource)
    {
        if (resource.ClientId == EryphConstants.SystemClientId)
            context.Fail(new AuthorizationFailureReason(this, "The system client cannot be edited or deleted."));
        
        context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
