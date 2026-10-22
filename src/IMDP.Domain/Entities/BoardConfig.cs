// =============================================================================
// 文件名：BoardConfig.cs
// 功能描述：板卡检测配置基表实体（分表合并版）。
//           通过 SqlSugar AS(tableName) 动态指定物理表名（{Category}BoardConfig），
//           1 个实体类管理 4 张物理表（GasDGBoardConfig/GasOGBoardConfig/WaterDWBoardConfig/WaterOWBoardConfig）。
//           Category 字段用 [SugarColumn(IsIgnore = true)] 不映射物理列，数据库零改动。
//           按产品编码维护板卡检测的工位数量与参数模板（单工位检测超时等扩展项随
//           参数模板 JSON 存储，不占物理列）。
// =============================================================================
using SqlSugar;

namespace IMDP.Domain.Entities;

/// <summary>
/// 板卡检测配置基表实体（分表合并版）
/// <para>对应基表名 BoardConfig；运行时通过 AS({Category}BoardConfig) 路由到具体物理表。</para>
/// <para>每个产品编码对应一份板卡检测参数模板。</para>
/// <para>实现 <see cref="IAuditable"/>，由 SqlSugar AOP 自动填充审计字段。</para>
/// </summary>
[SugarTable("BoardConfig")]
public class BoardConfig : IAuditable, IBoardConfig
{
    /// <summary>主键 Id（BIGINT IDENTITY 自增主键）</summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    /// <summary>产品编码（物料类别，长度 100）</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>物料编号（长度 100）</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string MaterialNumber { get; set; } = string.Empty;

    /// <summary>
    /// 方案所属插件的稳定标识，NULL或空值表示尚未标记插件归属
    /// </summary>
    [SugarColumn(ColumnDataType = "nvarchar", Length = 100, IsNullable = true)]
    public string? ProductKey { get; set; }

    /// <summary>检测参数模板 JSON（默认空数组，NVARCHAR(MAX)）</summary>
    [SugarColumn(ColumnDataType = "NVARCHAR(MAX)", IsNullable = false)]
    public string ParametersJson { get; set; } = "[]";

    /// <summary>工位数量（默认 4）</summary>
    public int StationCount { get; set; } = 4;

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

    /// <summary>
    /// 产品类别（GasDG/GasOG/WaterDW/WaterOW）
    /// <para>仅运行时使用，不映射数据库物理列（[SugarColumn(IsIgnore = true)]）。</para>
    /// <para>用于拼接物理表名 {Category}BoardConfig。</para>
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public string Category { get; set; } = string.Empty;
}
