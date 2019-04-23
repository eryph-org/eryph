using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Haipa.IdentityDb;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Server;

namespace Haipa.Modules.Identity
{
    internal class TenantMiddleware : IMiddleware
    {
        public Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var enabledTenants = new[] { "default" };

            foreach (var tenant in enabledTenants)
            {
                var segment = "/" + tenant;
                if (!context.Request.Path.StartsWithSegments(segment, out var path)) continue;

                context.Request.PathBase += segment;
                context.Request.Path = path;
                context.Request.HttpContext.Items.Add("tenantId", tenant);
                break;

            }

            return next(context);
        }
    }

    internal static class TenantMiddlewareExtensions
    {
        public static void UseTenant(this IApplicationBuilder app)
        {
            app.UseMiddleware<TenantMiddleware>();
        }

        public static void AddTenant(this IServiceCollection services)
        {

            services.AddSingleton<ITenantProvider, TenantProvider>();
            services.AddSingleton<IOptionsMonitor<OpenIddictServerOptions>, OpenIddictServerOptionsProvider>();
            services.AddSingleton<IConfigureOptions<OpenIddictServerOptions>, OpenIddictServerOptionsInitializer>();
            services.AddTransient<TenantMiddleware>();
        }
    }
}
