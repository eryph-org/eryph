using Eryph.IdentityDb;

namespace Eryph.Modules.Identity.Services;

public class ClientApplicationDescriptor : ApplicationDescriptor
{
    public string Certificate { get; set; }

    public ClientApplicationDescriptor()
    {
        IdentityApplicationType = IdentityApplicationType.Client;
    }   

    public override TDescriptor Clone<TDescriptor>()
    {
        var copy = base.Clone<TDescriptor>();

        if(copy is ClientApplicationDescriptor clientCopy)
            clientCopy.Certificate = Certificate;

        return copy;
    }
}