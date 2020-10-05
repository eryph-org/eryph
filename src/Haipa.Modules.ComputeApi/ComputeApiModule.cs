using JetBrains.Annotations;


namespace Haipa.Modules.ComputeApi
{
    [UsedImplicitly]
    public class ComputeApiModule : ApiModule<ComputeApiModule>
    {
        public override string Name => "Haipa.Modules.ComputeApi";
        public override string Path => "compute";

        public override string ApiName => "Compute Api";
        public override string AudienceName => "compute_api";
    }


}
