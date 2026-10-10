// =============================================================================
// 文件名：IDbContext.cs
// 功能描述：数据库上下文接口，封装 SqlSugar 客户端实例与建表、种子数据初始化契约。
// =============================================================================
using SqlSugar;

namespace IMDP.Domain.Interfaces;

/// <summary>
/// 数据库上下文接口
/// <para>封装 <see cref="ISqlSugarClient"/> 实例，并提供数据库建表与种子数据初始化能力。</para>
/// <para>由 Infrastructure 层的 SqlSugarDbContext 实现。</para>
/// </summary>
public interface IDbContext
{
    /// <summary>SqlSugar 客户端实例（供仓储执行查询与持久化）</summary>
    ISqlSugarClient Client { get; }

    /// <summary>确保数据库与表结构已创建（CodeFirst 建表）</summary>
    /// <returns>异步任务</returns>
    Task EnsureCreatedAsync();

    /// <summary>写入初始种子数据（如默认用户、全局配置等）</summary>
    /// <returns>异步任务</returns>
    Task SeedDataAsync();
}