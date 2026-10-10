// =============================================================================
// 文件名：UserRoleCodes.cs
// 功能描述：角色代码常量定义，位于 Domain 层供所有层引用。
//           中文显示名映射与转换方法位于 Application 层的 UserRoles 类中。
// =============================================================================
namespace IMDP.Domain.Users;

/// <summary>
/// 角色代码常量
/// <para>定义系统中所有角色的唯一代码标识，供 Domain、Infrastructure、
/// Application、Presentation 各层统一引用，避免硬编码字符串。</para>
/// <para>角色中文显示名与转换方法参见 <see cref="IMDP.Application.Users.UserRoles"/>。</para>
/// </summary>
public static class UserRoleCodes
{
    public const string SystemAdmin = "SystemAdmin";
    public const string Admin = "Admin";
    public const string Configurator = "Configurator";
    public const string Operator = "Operator";
}