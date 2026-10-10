// =============================================================================
// 文件名：LoginConfig.cs
// 功能描述：用户登录配置日志实体，对应数据库 LoginConfig 表。
//           记录每次工位登录的工位、工号、IP 地址、登录类型、登录结果、
//           失败原因、软件版本、登录时间与登出时间（成功与失败均记录）。
// =============================================================================
using SqlSugar;

namespace IMDP.Domain.Entities;

/// <summary>
/// 用户登录配置日志实体
/// <para>对应数据库表 LoginConfig，每次工位登录成功后写入一条记录，登出时回填登出时间。</para>
/// <para>实现 <see cref="IAuditable"/>，由 SqlSugar AOP 自动填充审计字段。</para>
/// </summary>
[SugarTable("LoginConfig")]
public class LoginConfig : IAuditable
{
    /// <summary>主键 Id（BIGINT IDENTITY 自增主键）</summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    /// <summary>登录工位名称（长度 50）</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string Station { get; set; } = string.Empty;

    /// <summary>登录工号（长度 50）</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string WorkNumber { get; set; } = string.Empty;

    /// <summary>登录客户端 IP 地址（本机 IPv4，长度 50，可空）</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? IpAddress { get; set; }

    /// <summary>登录类型/鉴权渠道（MES=在线校验，Offline=离线登录；见 LoginTypeCodes，长度 20，可空）</summary>
    [SugarColumn(Length = 20, IsNullable = true)]
    public string? LoginType { get; set; }

    /// <summary>登录结果（"成功"/"失败"，长度 10，可空）</summary>
    [SugarColumn(Length = 10, IsNullable = true)]
    public string? LoginResult { get; set; }

    /// <summary>失败原因（登录失败时记录 MES 错误信息或异常信息，长度 500，可空）</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ErrorInfo { get; set; }

    /// <summary>登录时客户端软件版本号（长度 50）</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string SoftVer { get; set; } = string.Empty;

    /// <summary>登录时间</summary>
    public DateTime LoginTime { get; set; }

    /// <summary>登出时间（可空，用户登出时回填）</summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? LogoutTime { get; set; }

    /// <summary>机器名（由 SqlSugar AOP 自动填充本机 NetBIOS 名，长度 128）</summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>系统当前登录用户名（由 SqlSugar AOP 自动填充，长度 128）</summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>应用名称（由 SqlSugar AOP 自动填充，可空，长度 128）</summary>
    [SugarColumn(Length = 128, IsNullable = true)]
    public string? AppName { get; set; }
}
