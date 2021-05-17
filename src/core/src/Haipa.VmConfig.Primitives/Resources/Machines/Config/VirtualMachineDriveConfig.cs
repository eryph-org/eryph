namespace Haipa.Primitives.Resources.Machines.Config
{
    public class VirtualMachineDriveConfig
    {
        public string Name { get; set; }
        public string ShareSlug { get; set; }
        public string DataStore { get; set; }

        public string Template { get; set; }

        public int? Size { get; set; }
        public VirtualMachineDriveType? Type { get; set; }
    }
}