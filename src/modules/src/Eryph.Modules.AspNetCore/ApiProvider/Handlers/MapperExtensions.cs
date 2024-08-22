using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Eryph.Core;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers;

public static class MapperExtensions
{
    private const string AuthContextKey = "ERYPH_AUTH_CONTEXT";

    public static AuthContext GetAuthContext(this ResolutionContext context)
    {
        if (context.Items.TryGetValue(AuthContextKey, out var value) && value is AuthContext authContext)
            return authContext;

        throw new InvalidOperationException(
            $"The {nameof(AuthContext)} is not present in the {nameof(ResolutionContext)}");
    }

    public static void SetAuthContext(this IMappingOperationOptions options, AuthContext authContext)
    {
        options.Items[AuthContextKey] = authContext;
    }
}
