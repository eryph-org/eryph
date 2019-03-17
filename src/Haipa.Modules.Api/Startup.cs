using System.Linq;
using Haipa.Messages;
using Haipa.Modules.Api.Services;
using Haipa.Rebus;
using Haipa.StateDb;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Rebus.Handlers;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Rebus.Serialization.Json;
using SimpleInjector;
using SimpleInjector.Integration.AspNetCore.Mvc;
using SimpleInjector.Lifestyles;
using LogLevel = Rebus.Logging.LogLevel;

namespace Haipa.Modules.Api
{
    public class Startup : StartupBase
    {
        private readonly Container _globalContainer;
        private readonly Container _container = new Container();

        public Startup(IConfiguration configuration, Container globalContainer)
        {
            Configuration = configuration;
            _globalContainer = globalContainer;
        }

        public IConfiguration Configuration { get; }



        // This method gets called by the runtime. Use this method to add services to the container.

        public override void ConfigureServices(IServiceCollection services)
        {

            services.AddDbContext<StateStoreContext>(options => 
                _globalContainer.GetInstance<IDbContextConfigurer<StateStoreContext>>().Configure(options));

            services.AddMvc(op =>
                {
                    op.EnableEndpointRouting = false; 
                })
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                .AddApplicationPart(typeof(Startup).Assembly)
                .AddApplicationPart(typeof(VersionedMetadataController).Assembly);

            services.AddApiVersioning(options =>
            {
                options.ReportApiVersions = true;
                options.AssumeDefaultVersionWhenUnspecified = true;
            });
            services.AddOData().EnableApiVersioning();

            IntegrateSimpleInjector(services);
        }

        private void IntegrateSimpleInjector(IServiceCollection services)
        {
            _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.AddSingleton<IControllerActivator>(
                new SimpleInjectorControllerActivator(_container));
            services.AddSingleton<IViewComponentActivator>(
                new SimpleInjectorViewComponentActivator(_container));

            services.EnableSimpleInjectorCrossWiring(_container);
            services.UseSimpleInjectorAspNetRequestScoping(_container);
        }

        public override void Configure(IApplicationBuilder app)
        {
            var env = app.ApplicationServices.GetService<IHostingEnvironment>();
            var modelBuilder = app.ApplicationServices.GetService<VersionedODataModelBuilder>();

            Configure(app, env,modelBuilder);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, 
            IHostingEnvironment env, 
            VersionedODataModelBuilder modelBuilder)
        {
            InitializeContainer(app);

            //if (env.IsDevelopment())
            //{
            //    app.UseDeveloperExceptionPage();
            //}

            _container.Verify();

            app.UseMvc(b =>

            {
                b.Select().Expand().Filter().OrderBy().MaxTop(100).Count();
                var models = modelBuilder.GetEdmModels().ToArray();
                app.UseMvc( routes =>
                {
                    routes.MapVersionedODataRoutes("odata", "odata", models);
                    routes.MapVersionedODataRoutes( "odata-bypath", "odata/v{version:apiVersion}", models );
                });
                
            });

        }

        private void InitializeContainer(IApplicationBuilder app)
        {
            _container.Collection.Register(typeof(IHandleMessages<>), typeof(Startup).Assembly);
            _container.Register<IOperationManager, OperationManager>(Lifestyle.Scoped);


            _container.ConfigureRebus(configurer => configurer                
                .Transport(t => _globalContainer.GetInstance<IRebusTransportConfigurer>().ConfigureAsOneWayClient(t))
                    .Routing(x => x.TypeBased()
                    .MapAssemblyOf<ConvergeVirtualMachineCommand>("haipa.controller"))
                .Options(x =>
                {
                    x.SimpleRetryStrategy();
                    x.SetNumberOfWorkers(5);
                })
                .Serialization(x => x.UseNewtonsoftJson(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None }))
                .Logging(x => x.ColoredConsole(LogLevel.Debug)).Start());


            // Add application presentation components:
            _container.RegisterMvcControllers(app);
            _container.RegisterMvcViewComponents(app);

            // Add application services. For instance:
            //container.Register<IUserService, UserService>(Lifestyle.Scoped);

            // Allow Simple Injector to resolve services from ASP.NET Core.
            _container.AutoCrossWireAspNetComponents(app);
        }
    }


}
