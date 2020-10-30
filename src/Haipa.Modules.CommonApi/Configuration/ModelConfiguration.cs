using Haipa.Modules.AspNetCore.ApiProvider.Model.V1;
using Haipa.Modules.CommonApi.Controllers;
using Haipa.Modules.CommonApi.Models.V1;
using JetBrains.Annotations;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Mvc;

namespace Haipa.Modules.CommonApi.Configuration
{
    public class ODataModelConfiguration : IModelConfiguration
    {
        private static void ConfigureV1( ODataModelBuilder builder )
        {
            builder.Namespace = "Common";

            builder.EntitySet<Operation>("Operations");

        }

        public void Apply( ODataModelBuilder builder, ApiVersion apiVersion )
        {
            switch ( apiVersion.MajorVersion )
            {
                default:
                    ConfigureV1( builder );
                    break;
            }
        }
    }
}
