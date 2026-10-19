// =============================================================================
// 文件名：AppUpdateRecord.cs
// 功能描述：应用版本更新记录实体，对应数据库 AppUpdateRecords 表。
//           跟踪单台客户端从当前版本升级到目标版本的全过程与状态。
// =============================================================================
using SqlSugar;

namespace IMDP.Domain.Entities;

/// <summary>
/// 应用版本更新记录实体
/// <para>对应数据库表 AppUpdateRecords，记录一次客户端应用升级请求的完整生命周期。</para>
/// <para>实现 <see cref="IAuditable"/>，由 SqlSugar AOP 自动填充审计字段。</para>
/// </summary>
[SugarTable("AppUpdateRecords")]
public class AppUpdateRecord : IAuditable
{
    /// <summary>主键 Id（BIGINT IDENTITY 自增主键）</summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    /// <summary>当前版本号（升级前客户端版本，长度 50）</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CurrentVersion { get; set; } = string.Empty;

    /// <summary>目标版本号（升级后预期版本，长度 50）</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string TargetVersion { get; set; } = string.Empty;

    /// <summary>版本发布说明（可空，最大 500 字符）</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>发起升级的客户端机器名（长度 50）</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string ClientMachineName { get; set; } = string.Empty;

    /// <summary>客户端 IP 地址（可空，长度 50）</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string ClientIpAddress { get; set; } = string.Empty;

    /// <summary>升级包存储路径（可空，长度 256）</summary>
    [SugarColumn(Length = 256, IsNullable = true)]
    public string UpdatePackagePath { get; set; } = string.Empty;

    /// <summary>升级状态（待处理/进行中/已完成/失败，长度 20，默认 待处理）</summary>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string UpdateStatus { get; set; } = "待处理";

    /// <summary>升级开始时间（可空，仅在进入下载/安装阶段时填充）</summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? UpdateTime { get; set; }

    /// <summary>升级完成时间（可空；成功记录留空，待升级后软件自动重启时回填为本次启动时间；失败记录为失败时刻）</summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? CompletedTime { get; set; }

    /// <summary>升级失败时的错误信息（可空，NVARCHAR(MAX)）</summary>
    [SugarColumn(ColumnDataType = "NVARCHAR(MAX)", IsNullable = true)]
    public string ErrorMessage { get; set; } = string.Empty;

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