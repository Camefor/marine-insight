namespace MarineInsight.Web.Authentication;

/// <summary>
/// 后台管理员身份配置。此处邮箱须与 AddAdministratorRole 迁移中
/// 固化的管理员邮箱保持一致，二者共同保证该邮箱是唯一管理员。
/// </summary>
public sealed class AdminOptions
{
    /// <summary>角色名与授权策略、迁移种子保持一致。</summary>
    public const string AdministratorRoleName = "Administrator";

    public string? Email { get; init; }
}
