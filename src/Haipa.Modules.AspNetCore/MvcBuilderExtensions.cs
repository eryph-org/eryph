using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;

namespace Haipa.Modules.AspNetCore
{
    public static class MvcBuilderExtensions
    {
        public static IMvcBuilder ConfigureAsRazorModule(this IMvcBuilder builder)
        {

            var frame = new StackFrame(1);
            var method = frame.GetMethod();
            var type = method.DeclaringType;

            if (type == null)
                return builder;

            var moduleAssembly = type.Assembly;

            builder.ConfigureApplicationPartManager(apm =>
            {
                apm.ApplicationParts.Add(new AssemblyPart(moduleAssembly));

                var relatedAssemblies = RelatedAssemblyAttribute.GetRelatedAssemblies(moduleAssembly, false);
                foreach (var relatedAssembly in relatedAssemblies)
                {
                    var partFactory = ApplicationPartFactory.GetApplicationPartFactory(relatedAssembly);
                    foreach (var part in partFactory.GetApplicationParts(relatedAssembly))
                    {
                        apm.ApplicationParts.Add(part);
                    }
                }

            });
            return builder;
        }
    }
}
