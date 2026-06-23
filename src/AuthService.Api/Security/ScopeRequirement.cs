using Microsoft.AspNetCore.Authorization;

namespace AuthService.Api.Security;

public sealed class ScopeRequirement : IAuthorizationRequirement
{
    public ScopeRequirement(string scope)
    {
        Scope = scope;
    }

    public string Scope { get; }
}
