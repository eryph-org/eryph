namespace Eryph.IdentityDb.Entities;

public class ClientApplicationEntity : ApplicationEntity
{
    public ClientApplicationEntity()
    {
        IdentityApplicationType = IdentityApplicationType.Client;
    }

    public string Certificate { get; set; }
}
