using Haipa.StateDb.Model;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNetCore.Mvc;

namespace Haipa.Modules.CommonApi.Configuration
{
    public class ODataModelConfiguration : IModelConfiguration
    {
        private static void ConfigureV1( ODataModelBuilder builder )
        {
            builder.Namespace = "Common";

            builder.EntitySet<Operation>("Operations");
            builder.EntitySet<OperationLogEntry>("OperationLogs");
            builder.EntitySet<OperationTask>("OperationTaks");

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
