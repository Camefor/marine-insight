using MarineInsight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MarineInsight.Web.Authentication;

public static class AccountEndpointExtensions
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/account")
            .RequireRateLimiting("account");

        // Minimal API form binding adds antiforgery validation metadata to these handlers.
        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        [FromForm] RegisterRequest request,
        UserManager<MarineInsightUser> userManager,
        SignInManager<MarineInsightUser> signInManager)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.LocalRedirect("/account/register?error=invalid");
        }

        var user = new MarineInsightUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email
        };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var error = result.Errors.Any(item => item.Code.StartsWith("Password", StringComparison.Ordinal))
                ? "password"
                : "unavailable";
            return Results.LocalRedirect($"/account/register?error={error}");
        }

        if (userManager.Options.SignIn.RequireConfirmedEmail)
        {
            return Results.LocalRedirect("/account/login?status=confirmation-required");
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return Results.LocalRedirect(GetLocalReturnUrl(request.ReturnUrl));
    }

    private static async Task<IResult> LoginAsync(
        [FromForm] LoginRequest request,
        SignInManager<MarineInsightUser> signInManager)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.LocalRedirect("/account/login?error=invalid");
        }

        var result = await signInManager.PasswordSignInAsync(
            email,
            request.Password,
            request.RememberMe,
            lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return Results.LocalRedirect("/account/login?error=locked");
        }

        if (!result.Succeeded)
        {
            return Results.LocalRedirect("/account/login?error=invalid");
        }

        return Results.LocalRedirect(GetLocalReturnUrl(request.ReturnUrl));
    }

    private static async Task<IResult> LogoutAsync(
        [FromForm] LogoutRequest _,
        SignInManager<MarineInsightUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.LocalRedirect("/");
    }

    // Only local absolute paths are accepted so authentication cannot become an open redirect.
    private static string GetLocalReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)
            || returnUrl[0] != '/'
            || returnUrl.StartsWith("//", StringComparison.Ordinal)
            || returnUrl.StartsWith("/\\", StringComparison.Ordinal))
        {
            return "/";
        }

        return returnUrl;
    }

    public sealed class RegisterRequest
    {
        public string? Email { get; init; }

        public string? Password { get; init; }

        public string? ReturnUrl { get; init; }
    }

    public sealed class LoginRequest
    {
        public string? Email { get; init; }

        public string? Password { get; init; }

        public bool RememberMe { get; init; }

        public string? ReturnUrl { get; init; }
    }

    public sealed class LogoutRequest;
}
