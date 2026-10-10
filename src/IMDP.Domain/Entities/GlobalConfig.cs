// =============================================================================
// 文件名：GlobalConfig.cs
// 功能描述：全局配置实体，对应数据库 GlobalConfigs 表。
//           单例配置（固定 Id=1），存放串口通讯参数与默认物料编号等系统级设置。
// =============================================================================
using SqlSugar;

namespace IMDP.Domain.Entities;

/// <summary>
/// 全局配置实体
/// <para>对应数据库表 GlobalConfigs，全表仅一条记录（固定主键 Id=1）。</para>
/// <para>存放串口通讯默认参数、快捷指令 JSON 与当前选中物料编号。</para>
/// <para>实现 <see cref="IAuditable"/>，由 SqlSugar AOP 自动填充审计字段。</para>
/// <para>实现 <see cref="ISoftDeletable"/>，删除时置 IsDeleted=true，不物理删除。</para>
/// </summary>
[SugarTable("GlobalConfigs")]
public class GlobalConfig : IAuditable, ISoftDeletable
{
    /// <summary>主键 Id（固定值 1，非自增，保证全局单例语义）</summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = false)]
    public long Id { get; set; } = 1;

    /// <summary>波特率（默认 9600）</summary>
    public int BaudRate { get; set; } = 9600;

    /// <summary>数据位（默认 8）</summary>
    public int DataBits { get; set; } = 8;

    /// <summary>停止位（One/OnePointFive/Two，长度 20，默认 One）</summary>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string StopBits { get; set; } = "One";

    /// <summary>校验位（None/Even/Odd/Mark/Space，长度 20，默认 None）</summary>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string Parity { get; set; } = "None";

    /// <summary>快捷指令 JSON 数组（默认空数组，NVARCHAR(MAX)）</summary>
    [SugarColumn(ColumnDataType = "NVARCHAR(MAX)", IsNullable = false)]
    public string CommandsJson { get; set; } = "[]";

    /// <summary>当前默认物料编号（长度 100）</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string MaterialNumber { get; set; } = string.Empty;

    /// <summary>创建人（操作员工号，由应用层填充，长度 50）</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>最后更新人（操作员工号，由应用层填充，长度 50）</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string UpdatedBy { get; set; } = string.Empty;

    /// <summary>最后更新时间</summary>
    public DateTime UpdatedTime { get; set; }

    /// <summary>软删除标记（true 表示已删除）</summary>
    public bool IsDeleted { get; set; }

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
