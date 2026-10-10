// =============================================================================
// 文件名：Login.cs
// 功能描述：用户登录记录实体，对应数据库 LoginRecords 表。
//           记录每次工位登录的工位、工号、IP 地址、登录类型、登录结果、
//           失败原因、软件版本、登录时间与登出时间（成功与失败均记录）。
// =============================================================================
using SqlSugar;

namespace IMDP.Domain.Entities;

/// <summary>
/// 用户登录记录实体
/// <para>对应数据库表 LoginRecords，每次工位登录成功后写入一条记录，登出时回填登出时间。</para>
/// </summary>
[SugarTable("LoginRecords")]
public class Login
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

    /// <summary>登录时客户端软件版本号（长度 50）</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string SoftVer { get; set; } = string.Empty;

    /// <summary>登录时间</summary>
    [SugarColumn(IsNullable = false)]
    public DateTime LoginTime { get; set; }

    /// <summary>登出时间（可空，用户登出时回填）</summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? LogoutTime { get; set; }

    /// <summary>登录类型/鉴权渠道（MES=在线校验，Offline=离线登录；见 LoginTypeCodes，长度 255，可空）</summary>
    [SugarColumn(Length = 255, IsNullable = true)]
    public string? LoginType { get; set; }

    /// <summary>登录结果（"成功"/"失败"，长度 255，可空）</summary>
    [SugarColumn(Length = 255, IsNullable = true)]
    public string? LoginResult { get; set; }

    /// <summary>失败原因（登录失败时记录 MES 错误信息或异常信息，长度 255，可空）</summary>
    [SugarColumn(Length = 255, IsNullable = true)]
    public string? ErrorInfo { get; set; }

    /// <summary>登录客户端 IP 地址（本机 IPv4，长度 255，可空）</summary>
    [SugarColumn(Length = 255, IsNullable = true)]
    public string? IpAddress { get; set; }
}
