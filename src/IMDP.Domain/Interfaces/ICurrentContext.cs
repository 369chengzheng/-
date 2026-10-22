// =============================================================================
// 文件名：ICurrentContext.cs
// 功能描述：当前操作上下文接口，提供审计字段所需的计算机名、用户名、应用名。
//           在用户登录/登出时由应用层更新，供 SqlSugar AOP 拦截器读取以填充审计字段。
// =============================================================================
namespace IMDP.Domain.Interfaces;

/// <summary>
/// 当前操作上下文，提供审计字段所需的计算机名、用户名、应用名。
/// 在用户登录/登出时更新。
/// </summary>
public interface ICurrentContext
{
    /// <summary>当前机器名（本机 NetBIOS 名，用于审计填充）</summary>
    string ComputerName { get; }

    /// <summary>当前登录用户工号（用于审计填充）</summary>
    string UserName { get; }

    /// <summary>
    /// 角色
    /// </summary>
    string Role { get; }

    /// <summary>当前应用名称（用于审计填充，可空）</summary>
    string? AppName { get; }

    /// <summary>当前登录成功写入 LoginConfig 表的记录 Id（用于登出时回填登出时间；未登录为 null）</summary>
    long? CurrentLoginRecordId { get; }

    /// <summary>设置当前登录用户</summary>
    /// <param name="workNumber">登录用户工号</param>
    void SetUser(string workNumber, string role);

    /// <summary>记录当前登录写入 LoginConfig 表的记录 Id（用于登出时回填登出时间）</summary>
    /// <param name="loginRecordId">刚写入的登录记录主键 Id</param>
    void SetCurrentLoginRecordId(long loginRecordId);

    /// <summary>清除当前登录用户（登出时调用）</summary>
    void ClearUser();
}