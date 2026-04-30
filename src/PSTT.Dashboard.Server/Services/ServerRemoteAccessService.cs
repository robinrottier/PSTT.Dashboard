using Microsoft.Extensions.Configuration;
using PSTT.Dashboard.Services;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace PSTT.Dashboard.Server.Services;

/// <summary>
/// Server-side implementation of IRemoteAccessService.
/// Reads IConfiguration directly and generates a token via UserSettingsService if absent,
/// avoiding HTTP loopback calls.
/// </summary>
public class ServerRemoteAccessService : IRemoteAccessService
{
    private readonly IConfiguration _configuration;
    private readonly UserSettingsService _userSettings;

    public ServerRemoteAccessService(IConfiguration configuration, UserSettingsService userSettings)
    {
        _configuration = configuration;
        _userSettings = userSettings;
    }

    public async Task<RemoteAccessTokenDto> GetOrCreateTokenAsync()
    {
        var token = _configuration["RemoteAccess:ApiToken"];
        if (string.IsNullOrEmpty(token))
            token = await GenerateAndStoreTokenAsync();

        var hasAdminAuth = !string.IsNullOrEmpty(_configuration["Auth:AdminPasswordHash"]);
        var warning = hasAdminAuth ? null
            : "Remote access token is only effective when admin authentication is configured.";

        return new RemoteAccessTokenDto(token, warning);
    }

    private async Task<string> GenerateAndStoreTokenAsync()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        await _userSettings.UpdateAsync(root =>
        {
            if (root["RemoteAccess"] is not JsonObject ra)
            {
                ra = new JsonObject();
                root["RemoteAccess"] = ra;
            }
            ra["ApiToken"] = token;
        });

        return token;
    }
}
