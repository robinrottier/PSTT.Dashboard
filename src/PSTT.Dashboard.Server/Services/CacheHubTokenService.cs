using Microsoft.AspNetCore.DataProtection;

namespace PSTT.Dashboard.Server.Services;

/// <summary>
/// Issues and validates cryptographically signed presence tokens for the <c>/cachehub</c>
/// SignalR endpoint.
///
/// Tokens are signed via ASP.NET Core Data Protection (keys are persisted to disk, so
/// existing browser cookies remain valid across server restarts). Any valid token proves
/// the bearer obtained it from this server — external scanners that never load a page will
/// not have a token.
///
/// This is not a full authentication system; it is a lightweight barrier against internet
/// scanners and bots that directly probe <c>/cachehub</c> without first loading the app.
/// </summary>
public sealed class CacheHubTokenService
{
    private readonly ITimeLimitedDataProtector _protector;

    // Sentinel plaintext — anything that proves the token came from us.
    private const string Payload = "chp1";

    /// <summary>How long an issued token (and its cookie) remains valid.</summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(7);

    public CacheHubTokenService(IDataProtectionProvider dataProtection)
    {
        _protector = dataProtection
            .CreateProtector("PSTT.Dashboard.CacheHub.Presence.v1")
            .ToTimeLimitedDataProtector();
    }

    /// <summary>
    /// Creates a new signed presence token valid for <see cref="TokenLifetime"/>.
    /// Each call produces a distinct ciphertext but all are validatable by this server.
    /// </summary>
    public string IssueToken() => _protector.Protect(Payload, lifetime: TokenLifetime);

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="token"/> was issued by this server
    /// and has not expired.
    /// </summary>
    public bool Validate(string? token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        try { return _protector.Unprotect(token) == Payload; }
        catch { return false; }
    }
}
