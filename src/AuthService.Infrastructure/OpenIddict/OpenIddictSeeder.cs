using OpenIddict.Abstractions;

namespace AuthService.Infrastructure.OpenIddict;

public sealed class OpenIddictSeeder
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictScopeManager _scopeManager;

    public OpenIddictSeeder(
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager
    )
    {
        _applicationManager = applicationManager;
        _scopeManager = scopeManager;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await CreateScopeAsync(cancellationToken);
        await CreateClientAsync(cancellationToken);
    }

    private async Task CreateScopeAsync(CancellationToken cancellationToken)
    {
        await CreateScopeIfNotExistsAsync(
            name: "orders-api",
            displayName: "Orders API",
            resources: ["orders-api"],
            cancellationToken);

        await CreateScopeIfNotExistsAsync(
            name: "orders.read",
            displayName: "Read orders",
            resources: ["orders-api"],
            cancellationToken);

        await CreateScopeIfNotExistsAsync(
            name: "orders.write",
            displayName: "Write orders",
            resources: ["orders-api"],
            cancellationToken);

        await CreateScopeIfNotExistsAsync(
            name: "users-api",
            displayName: "Users API",
            resources: ["users-api"],
            cancellationToken);

        await CreateScopeIfNotExistsAsync(
            name: "users.read",
            displayName: "Read users",
            resources: ["users-api"],
            cancellationToken);

        await CreateScopeIfNotExistsAsync(
            name: "users.manage",
            displayName: "Manage users",
            resources: ["users-api"],
            cancellationToken);
    }

    private async Task CreateScopeIfNotExistsAsync(
        string name,
        string displayName,
        string[] resources,
        CancellationToken cancellationToken)
    {
        if (await _scopeManager.FindByNameAsync(name, cancellationToken) is not null)
        {
            return;
        }

        await _scopeManager.CreateAsync(
            new OpenIddictScopeDescriptor
            {
                Name = name,
                DisplayName = displayName,
                Resources =
                {
                    resources[0]
                }
            },
            cancellationToken);
    }

    private async Task CreateClientAsync(CancellationToken cancellationToken)
    {
        await CreateMobileAppAsync(cancellationToken);
        await CreateAdminPanelAsync(cancellationToken);
        await CreateDebugClientAsync(cancellationToken);
        await CreateDemoClientAsync(cancellationToken);
    }

    private async Task CreateMobileAppAsync(CancellationToken cancellationToken)
    {
        const string clientId = "mobile-app";

        if (await _applicationManager.FindByClientIdAsync(clientId, cancellationToken) is not null)
        {
            return;
        }

        await _applicationManager.CreateAsync(
            new OpenIddictApplicationDescriptor
            {
                ClientId = clientId,
                DisplayName = "Mobile App",
                ClientType = OpenIddictConstants.ClientTypes.Public,
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
                RedirectUris =
                {
                    new Uri("com.company.mobile:/callback")
                },

                PostLogoutRedirectUris =
                {
                    new Uri("com.company.mobile:/logout-callback")
                },

                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.Endpoints.EndSession,

                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken,

                    OpenIddictConstants.Permissions.ResponseTypes.Code,

                    OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OpenId,
                    OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Email,
                    OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess,

                    OpenIddictConstants.Permissions.Prefixes.Scope + "orders.read",
                    OpenIddictConstants.Permissions.Prefixes.Scope + "orders.write"
                },

                Requirements =
                {
                    OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange
                }
            },
            cancellationToken);
    }

    private async Task CreateAdminPanelAsync(CancellationToken cancellationToken)
    {
        const string clientId = "admin-panel";

        if (await _applicationManager.FindByClientIdAsync(clientId, cancellationToken) is not null)
        {
            return;
        }

        await _applicationManager.CreateAsync(
            new OpenIddictApplicationDescriptor
            {
                ClientId = clientId,
                ClientSecret = "dev-admin-secret",
                DisplayName = "Admin Panel",
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
                RedirectUris =
                {
                    new Uri("https://localhost:5003/signin-oidc")
                },
                PostLogoutRedirectUris =
                {
                    new Uri("https://localhost:5003/signout-callback-oidc")
                },
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.Endpoints.EndSession,

                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken,

                    OpenIddictConstants.Permissions.ResponseTypes.Code,

                    OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OpenId,
                    OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Email,
                    OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess,

                    OpenIddictConstants.Permissions.Prefixes.Scope + "users.read",
                    OpenIddictConstants.Permissions.Prefixes.Scope + "users.manage",
                    OpenIddictConstants.Permissions.Prefixes.Scope + "orders.read"
                },
                Requirements =
                {
                    OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange
                }
            },
            cancellationToken);
    }

    private async Task CreateDebugClientAsync(CancellationToken cancellationToken)
    {
        const string clientId = "debug-client";

        if (await _applicationManager.FindByClientIdAsync(clientId, cancellationToken) is not null)
        {
            return;
        }

        await _applicationManager.CreateAsync(
                new OpenIddictApplicationDescriptor
                {
                    ClientId = clientId,
                    DisplayName = "Debug Client",
                    ClientType = OpenIddictConstants.ClientTypes.Public,
                    ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
                    RedirectUris =
                    {
                        new Uri("http://localhost:5058/debug/callback")
                    },
                    PostLogoutRedirectUris =
                    {
                        new Uri("http://localhost:5058/debug/logout-callback")
                    },

                    Permissions =
                    {
                        OpenIddictConstants.Permissions.Endpoints.Authorization,
                        OpenIddictConstants.Permissions.Endpoints.Token,
                        OpenIddictConstants.Permissions.Endpoints.EndSession,

                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                        OpenIddictConstants.Permissions.GrantTypes.RefreshToken,

                        OpenIddictConstants.Permissions.ResponseTypes.Code,

                        OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OpenId,
                        OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Profile,
                        OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Email,
                        OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess,

                        OpenIddictConstants.Permissions.Prefixes.Scope + "orders.read"
                    },

                    Requirements =
                    {
                        OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange
                    }
                },
                cancellationToken
                );
    }

    private async Task CreateDemoClientAsync(CancellationToken cancellationToken)
    {
        const string clientId = "demo-client";

        if (await _applicationManager.FindByClientIdAsync(clientId, cancellationToken) is not null)
        {
            return;
        }

        await _applicationManager.CreateAsync(
            new OpenIddictApplicationDescriptor
            {
                ClientId = clientId,
                DisplayName = "Visual Demo Client",
                ClientType = OpenIddictConstants.ClientTypes.Public,
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
                RedirectUris =
                {
                    new Uri("http://localhost:5058/debug/callback"),
                    new Uri("https://localhost:8443/debug/callback")
                },
                PostLogoutRedirectUris =
                {
                    new Uri("http://localhost:5058/debug/logout-callback"),
                    new Uri("https://localhost:8443/debug/logout-callback")
                },
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.Endpoints.EndSession,

                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken,

                    OpenIddictConstants.Permissions.ResponseTypes.Code,

                    OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OpenId,
                    OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Email,
                    OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess,

                    OpenIddictConstants.Permissions.Prefixes.Scope + "orders.read"
                },
                Requirements =
                {
                    OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange
                }
            },
            cancellationToken);
    }
}
