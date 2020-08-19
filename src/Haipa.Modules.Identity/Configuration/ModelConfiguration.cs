using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNetCore.Mvc;
using Haipa.Modules.Identity.Models.V1;
using JetBrains.Annotations;

namespace Haipa.Modules.Identity.Configuration
{
    [UsedImplicitly]
    public class ODataModelConfiguration : IModelConfiguration
    {
        private static void ConfigureV1(ODataModelBuilder builder)
        {
            builder.Namespace = "Haipa";
            builder.EntitySet<ClientApiModel>("Clients");
            builder.EntityType<ClientApiModel>().Name = "Client";

        }
        public void Apply(ODataModelBuilder builder, ApiVersion apiVersion)
        {
            switch (apiVersion.MajorVersion)
            {
                default:
                    ConfigureV1(builder);
                    break;
            }
        }
    }
}
