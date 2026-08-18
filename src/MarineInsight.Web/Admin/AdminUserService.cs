using MarineInsight.Application.Admin;
using MarineInsight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MarineInsight.Web.Admin;

/// <summary>
/// 后台用户只读查询：直接投影 Identity 用户表，不做任何写操作。
/// </summary>
public sealed class AdminUserService
{
    private readonly UserManager<MarineInsightUser> _userManager;

    public AdminUserService(UserManager<MarineInsightUser> userManager)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    }

    public async Task<IReadOnlyList<AdminUserSummary>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(user => user.UserName)
            .Select(user => new AdminUserSummary(
                user.Id,
                user.UserName ?? string.Empty,
                user.EmailConfirmed,
                user.LockoutEnabled,
                user.LockoutEnd,
                user.AccessFailedCount))
            .ToListAsync(cancellationToken);

        return users;
    }
}
