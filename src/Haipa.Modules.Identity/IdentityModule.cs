using System.Collections.Generic;
using Haipa.IdentityDb;
using Haipa.Modules.Identity.Services;
using Haipa.Modules.Identity.Swagger;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.Models;
using Microsoft.EntityFrameworkCore;
using SimpleInjector;

namespace Haipa.Modules.Identity
{
    using Microsoft.AspNet.OData;
    using Microsoft.AspNet.OData.Builder;
    using Microsoft.AspNet.OData.Extensions;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ApiExplorer;
    using Microsoft.AspNetCore.Mvc.Cors.Internal;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;
    using Swashbuckle.AspNetCore.SwaggerGen;
    using System;
    using System.Linq;


    /// <summary>
    /// Defines the <see cref="IdentityModule" />
    /// </summary>
    [ApiVersion("1.0")]

    public class IdentityModule : WebModule
    {
        /// <summary>
        /// Gets the Name
        /// </summary>
        public override string Name => "Haipa.Modules.Identity";

        /// <summary>
        /// Gets the Path
        /// </summary>
        public override string Path => "identity";

        public void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services)
        {
            services.AddMvc(op =>
            {
                op.EnableEndpointRouting = false;
                //var policy = new AuthorizationPolicyBuilder()
                //    .RequireAuthenticatedUser()
                //    .Build();
                //op.Filters.Add(new AuthorizeFilter(policy));
            })
               .SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
               .AddApplicationPart(typeof(IdentityModule).Assembly)
               .AddApplicationPart(typeof(VersionedMetadataController).Assembly);

            services.AddIdentityServer()
                .AddJwtBearerClientAuthentication()
                .AddDeveloperSigningCredential()
                
                .AddConfigurationStore(options =>
                {
                    options.ConfigureDbContext = builder =>
                        serviceProvider.GetService<IDbContextConfigurer<ConfigurationDbContext>>().Configure(builder);
                })
                .AddInMemoryApiResources(new List<ApiResource>
                {               
                    new ApiResource("identity:apps:read:all"),
                    new ApiResource("compute_api")
                })
                //.AddInMemoryCaching()
                .AddInMemoryIdentityResources(
                    new[]
                    {
                        new IdentityResources.OpenId(),
                    });


            services.AddApiVersioning(options =>
            {
                options.ReportApiVersions = true;
                options.AssumeDefaultVersionWhenUnspecified = false;
            });
            services.AddOData().EnableApiVersioning();

            services.AddODataApiExplorer(
                options =>
                {
                    // add the versioned api explorer, which also adds IApiVersionDescriptionProvider service
                    // note: the specified format code will format the version as "'v'major[.minor][-status]"
                    options.GroupNameFormat = "'v'VVV";

                    // note: this option is only necessary when versioning by url segment. the SubstitutionFormat
                    // can also be used to control the format of the API version in route templates
                    options.SubstituteApiVersionInUrl = true;


                });
            services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
            services.AddSwaggerGen(
                options =>
                {
                    // add a custom operation filter which sets default values
                    options.OperationFilter<SwaggerDefaultValues>();

                    //// integrate xml comments
                    //options.IncludeXmlComments(XmlCommentsFilePath);

                    options.ResolveConflictingActions(app => app.First());
                    options.EnableAnnotations();
                    options.DescribeAllEnumsAsStrings();
                });
            //JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
            //JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();

            //services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            //    .AddJwtBearer(options =>
            //    {
            //        options.Authority = "https://localhost:62189/identity";
            //        options.Audience = "identity_api";
            //        options.RequireHttpsMetadata = false;
            //    });


            ////services.AddAuthorization(options =>
            ////{
            ////    options.AddPolicy("identity:apps:read:all", policy => policy.Requirements.Add(new HasScopeRequirement("identity:apps:read:all", "http://localhost:62189/identity")));
            ////});
            ////services.AddAuthorization(options =>
            ////{
            ////    options.AddPolicy("identity:apps:write:all", policy => policy.Requirements.Add(new HasScopeRequirement("identity:apps:write:all", "http://localhost:62189/identity")));
            ////});

            services.AddSingleton<IAuthorizationHandler, HasScopeHandler>();
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials());
            });
            services.Configure<MvcOptions>(options =>
            {
                 options.Filters.Add(new CorsAuthorizationFilterFactory("CorsPolicy"));
            });
        }

        public void Configure(IApplicationBuilder app)
        {
            //app.UseCors("CorsPolicy");
            //app.UseCorsMiddleware();

            var modelBuilder = app.ApplicationServices.GetService<VersionedODataModelBuilder>();
            var provider = app.ApplicationServices.GetService<IApiVersionDescriptionProvider>();
            app.UseIdentityServer();
            app.UseAuthentication();
            app.UseMvc(b =>
            {
                b.Select().Expand().Filter().OrderBy().MaxTop(100).Count();
                var models = modelBuilder.GetEdmModels().ToArray();
                app.UseMvc(routes =>
                {
                    //routes.MapVersionedODataRoutes("odata", "odata", models);
                    routes.MapVersionedODataRoutes("odata-bypath", "odata/v{version:apiVersion}", models);
                });

            });

            app.UseSwagger();
            app.UseSwaggerUI(
                options =>
                {
                    options.DisplayOperationId();

                    // build a swagger endpoint for each discovered API version
                    foreach (var description in provider.ApiVersionDescriptions)
                    {
                        options.SwaggerEndpoint($"/identity/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());

                    }
                });
        }

        public override void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {
            base.ConfigureContainer(serviceProvider, container);

            container.Register<IClientRepository, ClientRepository<ConfigurationDbContext>>(Lifestyle.Scoped);
            container.Register<IIdentityServerClientService, IdentityServerClientService>(Lifestyle.Scoped);
            container.Register<IClientService, ClientService>(Lifestyle.Scoped);

        }
    }
}
