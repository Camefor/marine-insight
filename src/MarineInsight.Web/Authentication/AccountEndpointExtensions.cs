using System.Net.Mail;
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
        CaptchaService captcha,
        UserManager<MarineInsightUser> userManager,
        SignInManager<MarineInsightUser> signInManager)
    {
        // 验证码在进入 Identity 逻辑前校验，失败不触发密码锁定计数。
        if (captcha.Enabled && !captcha.Validate(request.CaptchaId, request.CaptchaCode))
        {
            return Results.LocalRedirect("/account/register?error=captcha");
        }

        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email)
            || !MailAddress.TryCreate(email, out _)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.LocalRedirect("/account/register?error=invalid");
        }

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return Results.LocalRedirect("/account/register?error=confirm");
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
            // 用户名与邮箱相同，重复注册命中 DuplicateUserName/DuplicateEmail 时单独提示已注册。
            var error = result.Errors.Any(item => item.Code.StartsWith("Password", StringComparison.Ordinal))
                ? "password"
                : result.Errors.Any(item => item.Code is "DuplicateUserName" or "DuplicateEmail")
                    ? "email-exists"
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
        CaptchaService captcha,
        SignInManager<MarineInsightUser> signInManager)
    {
        // 验证码在密码校验前消费，错误验证码不触发锁定计数。
        if (captcha.Enabled && !captcha.Validate(request.CaptchaId, request.CaptchaCode))
        {
            return Results.LocalRedirect("/account/login?error=captcha");
        }

        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email)
            || !MailAddress.TryCreate(email, out _)
            || string.IsNullOrWhiteSpace(request.Password))
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

        public string? ConfirmPassword { get; init; }

        public string? CaptchaId { get; init; }

        public string? CaptchaCode { get; init; }

        public string? ReturnUrl { get; init; }
    }

    public sealed class LoginRequest
    {
        public string? Email { get; init; }

        public string? Password { get; init; }

        public bool RememberMe { get; init; }

        public string? CaptchaId { get; init; }

        public string? CaptchaCode { get; init; }

        public string? ReturnUrl { get; init; }
    }

    public sealed class LogoutRequest;
}
