// =============================================================================
// 文件名：MaterialProductMap.cs
// 功能描述：物料-产品插件映射实体，对应数据库 MaterialProductMaps 表。
//           维护物料编号与可加载产品插件列表的对应关系。
// =============================================================================
using SqlSugar;

namespace IMDP.Domain.Entities;

/// <summary>
/// 物料-产品插件映射实体
/// <para>对应数据库表 MaterialProductMaps，将一个物料编号映射到一组可加载的产品插件。</para>
/// <para>ProductPluginsJson 以 JSON 数组形式存储插件键名列表，应用启动时据此加载插件。</para>
/// <para>实现 <see cref="IAuditable"/>，由 SqlSugar AOP 自动填充审计字段。</para>
/// <para>实现 <see cref="ISoftDeletable"/>，删除时置 IsDeleted=true，不物理删除。</para>
/// </summary>
[SugarTable("MaterialProductMaps")]
public class MaterialProductMap : IAuditable, ISoftDeletable
{
    /// <summary>主键 Id（BIGINT IDENTITY 自增主键）</summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    /// <summary>物料编号（长度 100）</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string MaterialNumber { get; set; } = string.Empty;

    /// <summary>产品插件键名 JSON 数组（默认空数组，NVARCHAR(MAX)）</summary>
    [SugarColumn(ColumnDataType = "NVARCHAR(MAX)", IsNullable = false)]
    public string ProductPluginsJson { get; set; } = "[]";

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
