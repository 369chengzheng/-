// =============================================================================
// 文件名：Boards.cs
// 功能描述：板卡检测记录基表实体（分表合并版）。
//           通过 SqlSugar AS(tableName) 动态指定物理表名（{Category}Boards），
//           1 个实体类管理 4 张物理表（GasDGBoards/GasOGBoards/WaterDWBoards/WaterOWBoards）。
//           Category 字段用 [SugarColumn(IsIgnore = true)] 不映射物理列，数据库零改动。
//           记录单块板卡一次完整检测的物料、参数、结果与审计信息。
// =============================================================================
using SqlSugar;

namespace IMDP.Domain.Entities;

/// <summary>
/// 板卡检测记录基表实体（分表合并版）
/// <para>对应基表名 Boards；运行时通过 AS({Category}Boards) 路由到具体物理表。</para>
/// <para>每条记录代表一块板卡的一次检测过程。</para>
/// <para>实现 <see cref="IAuditable"/>，由 SqlSugar AOP 自动填充审计字段。</para>
/// </summary>
[SugarTable("Boards")]
public class Boards : IAuditable, IBoards
{
    /// <summary>主键 Id（BIGINT IDENTITY 自增主键）</summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    /// <summary>订单号（长度 100）</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>产品序列号（板卡唯一编号，长度 100）</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string ProductSn { get; set; } = string.Empty;

    /// <summary>产品编码（物料类别，长度 100）</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>物料编号（长度 100）</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string MaterialNumber { get; set; } = string.Empty;

    /// <summary>检测内容 JSON（包含各检测项的参数与结果，NVARCHAR(MAX)）</summary>
    [SugarColumn(ColumnDataType = "NVARCHAR(MAX)", IsNullable = false)]
    public string TestContentJson { get; set; } = string.Empty;

    /// <summary>检测结果（true=合格，false=不合格）</summary>
    public bool TestResult { get; set; }

    /// <summary>创建人（操作员工号，由应用层填充，长度 50）</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>机器名（由 SqlSugar AOP 自动填充本机 NetBIOS 名，长度 128）</summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>系统当前登录用户名（由 SqlSugar AOP 自动填充，长度 128）</summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>应用名称（由 SqlSugar AOP 自动填充，可空，长度 128）</summary>
    [SugarColumn(Length = 128, IsNullable = true)]
    public string? AppName { get; set; }

    /// <summary>
    /// 产品类别（GasDG/GasOG/WaterDW/WaterOW）
    /// <para>仅运行时使用，不映射数据库物理列（[SugarColumn(IsIgnore = true)]）。</para>
    /// <para>用于拼接物理表名 {Category}Boards。</para>
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public string Category { get; set; } = string.Empty;
}
