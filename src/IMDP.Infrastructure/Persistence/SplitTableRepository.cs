// =============================================================================
// 文件名：SplitTableRepository.cs
// 功能描述：分表仓储泛型基类
//           通过 SqlSugar AS(tableName) 动态指定物理表名，实现 1 个实体类管理多张物理表。
//           物理表名格式：{Category}{基表名}，如 GasDGBoardConfig。
//           子类重写 BaseTableName 属性提供基表名；Category 字段在实体上用 [SugarColumn(IsIgnore = true)] 不映射物理列。
//           数据库零改动：物理表已存在，无需 CodeFirst 建表。
// =============================================================================
using IMDP.Domain.Interfaces;
using SqlSugar;

namespace IMDP.Infrastructure.Persistence;

/// <summary>
/// 分表仓储泛型基类
/// <para>通过 SqlSugar AS(tableName) 动态指定物理表名，实现 1 个实体类管理多张物理表。</para>
/// <para>物理表名格式：{Category}{BaseTableName}，如 GasDGBoardConfig。</para>
/// <para>子类重写 <see cref="BaseTableName"/> 属性提供基表名；Category 字段在实体上用 <c>[SugarColumn(IsIgnore = true)]</c> 不映射物理列。</para>
/// </summary>
/// <typeparam name="T">实体类型，必须有无参构造函数</typeparam>
public abstract class SplitTableRepository<T> : Repository<T> where T : class, new()
{
    /// <summary>
    /// 初始化 <see cref="SplitTableRepository{T}"/> 类的新实例。
    /// </summary>
    /// <param name="dbContext">数据库上下文，提供 SqlSugar 客户端</param>
    protected SplitTableRepository(IDbContext dbContext) : base(dbContext) { }

    /// <summary>
    /// 基表名（不含产品类别前缀），由子类重写提供。
    /// <para>例如 BoardConfig 子类返回 "BoardConfig"，物理表名为 "GasDGBoardConfig" 等。</para>
    /// </summary>
    protected abstract string BaseTableName { get; }

    /// <summary>
    /// 按产品类别获取物理表名（如 GasDGBoardConfig）
    /// </summary>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW）</param>
    /// <returns>物理表名（{Category}{BaseTableName}）</returns>
    protected string GetTableName(string category)
        => $"{category}{BaseTableName}";

    /// <summary>
    /// 按产品类别创建可查询对象（自动 AS 到对应物理表）
    /// <para>示例：QueryByCategory("GasDG") 等价于 _db.Queryable&lt;BoardConfig&gt;().AS("GasDGBoardConfig")</para>
    /// </summary>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW）</param>
    /// <returns>已路由到物理表的可查询对象</returns>
    protected ISugarQueryable<T> QueryByCategory(string category)
        => _db.Queryable<T>().AS(GetTableName(category));
}
