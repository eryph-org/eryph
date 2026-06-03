using System;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    public class OpenSshChannelVMCommandResponse
    {
        public string Token { get; set; }
        public string AgentEndpoint { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
