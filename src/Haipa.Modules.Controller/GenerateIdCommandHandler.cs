using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Messages.Commands;
using Rebus.Bus;
using Rebus.Handlers;

namespace Haipa.Modules.Controller
{
    public class GenerateIdCommandHandler : IHandleMessages<GenerateIdCommand>
    {
        private readonly IBus _bus;
        private readonly Id64Generator _idGenerator;

        public GenerateIdCommandHandler(IBus bus, Id64Generator idGenerator)
        {
            _bus = bus;
            _idGenerator = idGenerator;
        }

        public Task Handle(GenerateIdCommand message)
        {
            var id = _idGenerator.GenerateId();
            return _bus.Reply(new GenerateIdReply {GeneratedId = id});

        }
    }
}