namespace PSTT.Dashboard.Services;

/// <summary>
/// Service for reading the remote API access token.
/// Server-side implementation reads IConfiguration directly (generating if absent);
/// WASM uses HttpClient.
/// </summary>
public interface IRemoteAccessService
{
    /// <summary>Gets the remote access token, creating and persisting one if not yet configured.</summary>
    Task<RemoteAccessTokenDto> GetOrCreateTokenAsync();
}

public record RemoteAccessTokenDto(string Token, string? Warning);
