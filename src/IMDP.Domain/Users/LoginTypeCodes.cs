// =============================================================================
// 文件名：LoginTypeCodes.cs
// 功能描述：登录类型代码常量定义，位于 Domain 层供所有层引用。
//           登录类型标识鉴权渠道（MES 在线校验 / 离线登录），写入 Logins 表 LoginType 列。
// =============================================================================
namespace IMDP.Domain.Users;

/// <summary>
/// 登录类型代码常量
/// <para>定义系统支持的登录鉴权渠道代码，供 Domain、Infrastructure、
/// Application、Presentation 各层统一引用，避免硬编码字符串。</para>
/// </summary>
public static class LoginTypeCodes
{
    /// <summary>MES 在线校验（调用 MES CheckUserInfo 校验工号/密码/资源编码）</summary>
    public const string Mes = "MES";

    /// <summary>离线登录（本地校验，预留扩展点，暂未启用）</summary>
    public const string Offline = "Offline";
}
