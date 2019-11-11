using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNetCore.Mvc;
using Haipa.Modules.Identity.Models;

namespace Haipa.Modules.Identity.Configuration
{
    public class ODataModelConfiguration : IModelConfiguration
    {
        private static void ConfigureV1(ODataModelBuilder builder)
        {
            builder.EntitySet<ClientEntityDTO>("ClientEntity");
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
