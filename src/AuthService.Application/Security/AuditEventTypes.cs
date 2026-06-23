namespace AuthService.Application.Security;

public static class AuditEventTypes
{
    public const string LoginSucceeded = "LoginSucceeded";
    public const string LoginFailed = "LoginFailed";

    public const string UserSessionCreated = "UserSessionCreated";
    public const string UserSessionRevoked = "UserSessionRevoked";
    public const string AllUserSessionsRevoked = "AllUserSessionsRevoked";

    public const string RefreshTokenRejected = "RefreshTokenRejected";
    public const string RefreshTokenUsed = "RefreshTokenUsed";
}
