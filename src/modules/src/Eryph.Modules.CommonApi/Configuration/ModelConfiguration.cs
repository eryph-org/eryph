//using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
//using Microsoft.AspNet.OData.Builder;
//using Microsoft.AspNetCore.Mvc;

//namespace Eryph.Modules.CommonApi.Configuration
//{
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
//            builder.Namespace = "Common";

//            builder.EntitySet<Operation>("Operations");
//        }
//    }
//}