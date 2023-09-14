using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json;
using OpenIddict.Abstractions;

namespace Eryph.IdentityDb.Entities;

public class ClientApplicationEntity : ApplicationEntity
{
    public ClientApplicationEntity()
    {
        IdentityApplicationType = IdentityApplicationType.Client;
    }

    public string Certificate { get; set; }



}