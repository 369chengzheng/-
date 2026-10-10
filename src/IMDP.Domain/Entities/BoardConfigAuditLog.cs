// =============================================================================
// 文件名：BoardConfigAuditLog.cs
// 功能描述：板卡配置变更审计日志基表实体（分表合并版）。
//           通过 SqlSugar AS(tableName) 动态指定物理表名（{Category}BoardConfigAuditLog），
//           1 个实体类管理 4 张物理表（GasDGBoardConfigAuditLog 等）。
//           Category 字段用 [SugarColumn(IsIgnore = true)] 不映射物理列，数据库零改动。
//           记录 BoardConfig 表的增删改前后数据快照。
// =============================================================================
using SqlSugar;

namespace IMDP.Domain.Entities;

/// <summary>
/// 板卡配置变更审计日志基表实体（分表合并版）
/// <para>对应基表名 BoardConfigAuditLog；运行时通过 AS({Category}BoardConfigAuditLog) 路由到具体物理表。</para>
/// <para>DeletedData 存放被删除前的原始数据快照，InsertedData 存放新增/修改后的数据快照。</para>
/// </summary>
[SugarTable("BoardConfigAuditLog")]
public class BoardConfigAuditLog : IBoardConfigAuditLog
{
    /// <summary>主键 Id（BIGINT IDENTITY 自增主键）</summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    /// <summary>关联的 BoardConfig 记录主键（BIGINT）</summary>
    public long RecordId { get; set; }

    /// <summary>操作类型（Insert/Update/Delete，长度 10）</summary>
    [SugarColumn(Length = 10, IsNullable = false)]
    public string OperationType { get; set; } = string.Empty;

    /// <summary>被删除/更新前的数据快照 JSON（可空，NVARCHAR(MAX)）</summary>
    [SugarColumn(ColumnDataType = "NVARCHAR(MAX)", IsNullable = true)]
    public string? DeletedData { get; set; }

    /// <summary>新增/更新后的数据快照 JSON（可空，NVARCHAR(MAX)）</summary>
    [SugarColumn(ColumnDataType = "NVARCHAR(MAX)", IsNullable = true)]
    public string? InsertedData { get; set; }

    /// <summary>操作人（操作员工号，由应用层填充，长度 50）</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>机器名（由应用层在写日志时填充本机 NetBIOS 名，长度 128）</summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>系统当前登录用户名（由应用层在写日志时填充，长度 128）</summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>应用名称（可空，长度 128）</summary>
    [SugarColumn(Length = 128, IsNullable = true)]
    public string? AppName { get; set; }

    /// <summary>日志写入时间（默认当前服务器时间）</summary>
    public DateTime CreatedTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 产品类别（GasDG/GasOG/WaterDW/WaterOW）
    /// <para>仅运行时使用，不映射数据库物理列（[SugarColumn(IsIgnore = true)]）。</para>
    /// <para>用于拼接物理表名 {Category}BoardConfigAuditLog。</para>
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public string Category { get; set; } = string.Empty;
}
