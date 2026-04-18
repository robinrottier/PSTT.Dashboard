using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MqttDashboard.Server.Services;
using System.Security.Claims;

namespace MqttDashboard.Server.Controllers;

[ApiController]
[Route("api/auth")]
[IgnoreAntiforgeryToken]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly LoginTokenStore _tokenStore;

    public AuthController(IConfiguration configuration, LoginTokenStore tokenStore)
    {
        _configuration = configuration;
        _tokenStore = tokenStore;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var readOnly = ReadOnlyHelper.IsReadOnly(_configuration, HttpContext);
        var authEnabled = !readOnly && !string.IsNullOrEmpty(_configuration["Auth:AdminPasswordHash"]);

        if (readOnly)
            return Ok(new { isAdmin = false, authEnabled = false, readOnly = true });

        if (!authEnabled)
            return Ok(new { isAdmin = true, authEnabled = false, readOnly = false });

        return Ok(new { isAdmin = User.Identity?.IsAuthenticated == true, authEnabled = true, readOnly = false });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var hash = _configuration["Auth:AdminPasswordHash"];
        if (string.IsNullOrEmpty(hash))
            return Ok(new { isAdmin = true }); // auth not configured

        if (string.IsNullOrEmpty(request.Password))
            return Unauthorized(new { error = "Password required" });

        bool valid;
        try { valid = BCrypt.Net.BCrypt.Verify(request.Password, hash); }
        catch { valid = false; }

        if (!valid)
            return Unauthorized(new { error = "Invalid password" });

        await SignInAsAdminAsync();
        return Ok(new { isAdmin = true });
    }

    /// <summary>
    /// Redeems a one-time login token issued by <see cref="ServerAuthService.GetLoginRedirectAsync"/>.
    /// Used by the Blazor Server login flow: the circuit validates the password and issues a token;
    /// the browser navigates here (forceLoad) so the cookie can be set on a live HTTP response.
    /// </summary>
    [HttpGet("redeem/{token}")]
    public async Task<IActionResult> RedeemLoginToken(string token, [FromQuery] string? returnUrl = null)
    {
        if (!_tokenStore.TryRedeem(token))
            return Redirect("/login?error=expired");

        await SignInAsAdminAsync();

        var redirect = (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            ? returnUrl
            : "/";

        return Redirect(redirect);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok();
    }

    private async Task SignInAsAdminAsync()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "admin"),
            new(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });
    }
}

public record LoginRequest(string Password);
