using IMDP.Domain.Users;

namespace IMDP.Application.Users;

public static class UserRoles
{
    //系统管理员：全部菜单
    //管理员：方案配置 + 检测 + 用户管理 + 查询类菜单
    //配置员：方案配置 + 检测 + 查询类菜单
    //作业员：检测 + 查询类菜单
    public static IReadOnlyDictionary<string, string> DisplayNames { get; } =
        new Dictionary<string, string>
        {
            [UserRoleCodes.SystemAdmin] = "系统管理员",
            [UserRoleCodes.Admin] = "管理员",
            [UserRoleCodes.Configurator] = "配置员",
            [UserRoleCodes.Operator] = "作业员"
        };

    /// <summary>
    /// 映射成中文名
    /// </summary>
    /// <param name="role"></param>
    /// <returns></returns>
    public static string GetDisplayName(string role) =>
        DisplayNames.TryGetValue(role, out var displayName) ? displayName : role;

    /// <summary>
    /// 将角色代码或中文显示名统一转换为角色代码
    /// </summary>
    /// <param name="roleNameOrCode">角色代码（如 SystemAdmin）或中文显示名（如 系统管理员），不区分大小写</param>
    public static string? GetCode(string roleNameOrCode)
    {
        var code = DisplayNames.Keys.FirstOrDefault(
            role => string.Equals(role, roleNameOrCode, StringComparison.OrdinalIgnoreCase));
        if (code != null) return code;

        return DisplayNames.FirstOrDefault(pair => pair.Value == roleNameOrCode).Key;
    }
}
