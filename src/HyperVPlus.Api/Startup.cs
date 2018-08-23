using System;
using HyperVPlus.Messages;
using HyperVPlus.Rebus;
using HyperVPlus.StateDb;
using HyperVPlus.StateDb.Model;
using HyperVPlus.StateDb.MySql;
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
using Microsoft.OData.Edm;
using Newtonsoft.Json;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Rebus.Serialization.Json;
using SimpleInjector;
using SimpleInjector.Integration.AspNetCore.Mvc;
using SimpleInjector.Lifestyles;
using LogLevel = Rebus.Logging.LogLevel;

namespace HyperVPlus.Api
{
    public class Startup
    {
        private readonly Container _container = new Container();

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<StateStoreContext>(options => options.UseMySql());
            services.AddOData();
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
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


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            InitializeContainer(app);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            _container.Verify();

            app.UseMvc(b =>

            {
                b.Select().Expand().Filter().OrderBy().MaxTop(100).Count();
                b.MapODataServiceRoute("odata", "odata", GetEdmModel());

            });


        }

        private void InitializeContainer(IApplicationBuilder app)
        {
            _container.Collection.Register(typeof(IHandleMessages<>), typeof(Program).Assembly);

            RabbitMqConnectionCheck.WaitForRabbitMq(new TimeSpan(0, 0, 10)).Wait();

            _container.ConfigureRebus(configurer => configurer
                .Transport(t => t.UseRabbitMqAsOneWayClient(RabbitMqConnectionCheck.ConnectionString))
                .Routing(x => x.TypeBased()
                    .Map<InitiateVirtualMachineConvergeCommand>("hypervplus.controller"))
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


        private static IEdmModel GetEdmModel()
        {

            ODataConventionModelBuilder builder = new ODataConventionModelBuilder();

            builder.EntitySet<Operation>("OperationSet");
            builder.EntitySet<OperationLog>("OperationLogSet");

            return builder.GetEdmModel();

        }
    }
}
