using Haipa.IdentityDb.Models;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;

namespace Haipa.Modules.Identity.Configuration
{
    public class ODataModelConfiguration : IModelConfiguration
    {
        private static void ConfigureV1(ODataModelBuilder builder)
        {
            builder.EntitySet<ClientEntity>("ClientEntity");
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
