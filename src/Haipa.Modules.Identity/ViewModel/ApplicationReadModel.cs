using System;
using System.Collections.Generic;
using System.Text;
using OpenIddict.Abstractions;

namespace Haipa.Modules.Identity.ViewModel
{
    public class ApplicationReadModel
    {
        public string ClientId { get; set; }
        public string DisplayName { get; set; }
        public IEnumerable<string> GrantTypes { get; set; }
        public IEnumerable<string> Scopes { get; set; }
        public string PostLogoutRedirectUris { get; set; }
        public string RedirectUris { get; set; }
    }

    public enum GrantType
    {
        ClientCredentials,
        AuthorizationCode,
        Implicit,
        Password,
        RefreshToken
    }
}
