using Microsoft.AspNetCore.Authorization;

namespace AuthService.Api.Security;

public sealed class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        if (HasScope(context, requirement.Scope))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool HasScope(AuthorizationHandlerContext context, string requiredScope)
    {
        return context.User.Claims.Any(claim =>
            claim.Type == AuthClaimTypes.OpenIddictScope && claim.Value == requiredScope
              || claim.Type == AuthClaimTypes.Scope && claim.Value
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains(requiredScope));
    }
}
