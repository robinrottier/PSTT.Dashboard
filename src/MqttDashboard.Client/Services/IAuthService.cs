namespace MqttDashboard.Services;

public interface IAuthService
{
    Task<(bool isAdmin, bool authEnabled, bool readOnly)> GetStatusAsync();
    Task<bool> LoginAsync(string password);
    Task LogoutAsync();

    /// <summary>
    /// Blazor Server login flow: validates the password and returns a one-time redirect URL
    /// that the browser should navigate to (with forceLoad) to complete cookie sign-in.
    /// Returns <c>null</c> if the password is invalid, or in WASM mode where
    /// <see cref="LoginAsync"/> handles the full flow directly.
    /// </summary>
    Task<string?> GetLoginRedirectAsync(string password);
}
