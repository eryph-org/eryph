using Eryph.ConfigModel;
using Eryph.Core.Genetics;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    public class PlaceCatletResult
    {
        [PrivateIdentifier]
        public string AgentName { get; set; }

        public Architecture Architecture { get; set; }
    }
}