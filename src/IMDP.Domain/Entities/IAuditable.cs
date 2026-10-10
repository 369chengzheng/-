// =============================================================================
// 文件名：IAuditable.cs
// 功能描述：审计字段接口，定义实体需提供的操作来源信息（机器名/用户名/应用名）。
//           由 SqlSugar AOP 拦截器在插入/更新时自动填充，无需业务代码手动赋值。
//           同时定义 ISoftDeletable 软删除接口，统一表达"逻辑删除"语义，
//           便于在仓储层添加通用的 IsDeleted == false 过滤逻辑。
// =============================================================================
namespace IMDP.Domain.Entities;

/// <summary>
/// 审计字段接口，由需要记录操作来源的实体实现。
/// 通过 SqlSugar AOP 在插入/更新时自动填充。
/// </summary>
public interface IAuditable
{
    /// <summary>机器名（执行操作的本机 NetBIOS 名，由 SqlSugar AOP 自动填充）</summary>
    string ComputerName { get; set; }

    /// <summary>系统当前登录用户名（由 SqlSugar AOP 自动填充）</summary>
    string UserName { get; set; }

    /// <summary>应用名称（可空，由 SqlSugar AOP 自动填充）</summary>
    string? AppName { get; set; }
}

/// <summary>
/// 软删除接口，由支持逻辑删除的实体实现。
/// <para>实现此接口的实体在删除时将 <see cref="IsDeleted"/> 置为 true，而非物理删除。</para>
/// <para>查询时仓储层应统一添加 <c>WHERE IsDeleted = 0</c> 过滤条件。</para>
/// </summary>
public interface ISoftDeletable
{
    /// <summary>软删除标记（true 表示已逻辑删除，不再参与业务查询）</summary>
    bool IsDeleted { get; set; }
}