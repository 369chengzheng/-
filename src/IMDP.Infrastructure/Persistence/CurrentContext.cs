// =============================================================================
// 文件名：CurrentContext.cs
// 功能描述：当前上下文实现
//           提供当前操作用户、计算机名、应用名等环境信息，
//           供 SqlSugar AOP 在插入/更新时自动填充审计字段（ComputerName/UserName/AppName）。
// =============================================================================
using System.Reflection;
using IMDP.Domain.Interfaces;

namespace IMDP.Infrastructure.Persistence;

/// <summary>
/// 当前上下文实现
/// <para>用于在仓储层与 SqlSugar AOP 中获取当前操作者及运行环境信息。</para>
/// </summary>
public class CurrentContext : ICurrentContext
{
    // 默认用户名为 "system"，表示系统级操作
    private string _userName = "system";
    private string _role = string.Empty;
    // 当前登录成功写入 LoginRecords 表的记录 Id（用于登出时回填登出时间）
    private long? _currentLoginRecordId;

    /// <summary>当前计算机名称（取自 Environment.MachineName）。</summary>
    public string ComputerName => Environment.MachineName;

    /// <summary>当前操作用户工号；未设置时返回 "system"。</summary>
    public string UserName => _userName;

    public string Role => _role;

    /// <summary>当前登录成功写入 LoginRecords 表的记录 Id（登出时回填登出时间用；未登录为 null）</summary>
    public long? CurrentLoginRecordId => _currentLoginRecordId;

    /// <summary>
    /// 当前应用程序名称。
    /// <para>取入口程序集名称；获取失败时返回默认值 "IMDP"。</para>
    /// </summary>
    public string? AppName =>
        Assembly.GetEntryAssembly()?.GetName().Name ?? "IMDP";

    /// <summary>
    /// 设置当前操作用户。
    /// </summary>
    /// <param name="workNumber">用户工号；为空时回退为 "system"。</param>
    public void SetUser(string workNumber, string role)
    {
        _userName = string.IsNullOrWhiteSpace(workNumber) ? "system" : workNumber;
        _role = role ?? string.Empty;
    }

    /// <summary>
    /// 记录当前登录写入 LoginRecords 表的记录 Id（登出时回填登出时间用）。
    /// </summary>
    /// <param name="loginRecordId">刚写入的登录记录主键 Id</param>
    public void SetCurrentLoginRecordId(long loginRecordId)
    {
        _currentLoginRecordId = loginRecordId;
    }

    /// <summary>
    /// 清除当前用户，回退为默认 "system"。
    /// </summary>
    public void ClearUser()
    {
        _userName = "system";
        _role = string.Empty;
        _currentLoginRecordId = null;
    }
}