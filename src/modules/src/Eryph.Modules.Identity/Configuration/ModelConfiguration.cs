//using Eryph.Modules.Identity.Models.V1;
//using JetBrains.Annotations;
//using Microsoft.AspNet.OData.Builder;
//using Microsoft.AspNetCore.Mvc;

//namespace Eryph.Modules.Identity.Configuration
//{
//    [UsedImplicitly]
//    public class ODataModelConfiguration : IModelConfiguration
//    {
//        public void Apply(ODataModelBuilder builder, ApiVersion apiVersion)
//        {
//            switch (apiVersion.MajorVersion)
//            {
//                default:
//                    ConfigureV1(builder);
//                    break;
//            }
//        }

//        private static void ConfigureV1(ODataModelBuilder builder)
//        {
//            builder.Namespace = "Eryph";
//            builder.EntitySet<Client>("Clients");
//            builder.EntityType<Client>().Name = "Client";
//        }
//    }
//}