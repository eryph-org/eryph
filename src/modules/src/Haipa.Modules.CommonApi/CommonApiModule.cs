using Haipa.Modules.AspNetCore;
using JetBrains.Annotations;

namespace Haipa.Modules.CommonApi
{
    [UsedImplicitly]
    public class CommonApiModule : ApiModule<CommonApiModule>
    {
        public override string Path => "common";

        public override string ApiName => "Common Api";
        public override string AudienceName => "common_api";
    }
}