﻿//using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
//using Eryph.Modules.ComputeApi.Model.V1;
//using Microsoft.AspNet.OData.Builder;
//using Microsoft.AspNetCore.Mvc;

//namespace Eryph.Modules.ComputeApi.Configuration
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
//            builder.Namespace = "Compute";

//            //operations are not part of this model and will therefore be no entity type.
//            builder.ComplexType<Operation>();

//            builder.EntitySet<Machine>("Machines");
//            builder.Action("CreateMachine").Returns<Operation>();

//            builder.EntityType<Machine>().Action("Start").Returns<Operation>();
//            builder.EntityType<Machine>().Action("Stop").Returns<Operation>();

//            builder.EntityType<Machine>().Action("Update").Returns<Operation>();


//            builder.EntitySet<VirtualMachine>("VirtualMachines");
//            builder.EntityType<VirtualMachine>().DerivesFromNothing(); //doesn't work with AutoMapper


//            builder.EntitySet<VirtualDisk>("VirtualDisks");
//        }
//    }
//}