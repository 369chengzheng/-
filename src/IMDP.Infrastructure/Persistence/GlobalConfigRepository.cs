// =============================================================================
// 文件名：GlobalConfigRepository.cs
// 功能描述：全局配置（GlobalConfig）仓储实现
//           继承泛型仓储基类，提供读取单例全局配置的方法。
//           全局配置表 GlobalConfigs 仅维护一条固定主键记录。
// =============================================================================
using IMDP.Domain.Entities;
using IMDP.Domain.Interfaces;

namespace IMDP.Infrastructure.Persistence;

/// <summary>
/// 全局配置仓储实现
/// <para>继承自 <see cref="Repository{T}"/>，提供读取全局配置表首条（单例）记录的方法。</para>
/// </summary>
public class GlobalConfigRepository : Repository<GlobalConfig>, IGlobalConfigRepository
{
    /// <summary>
    /// 初始化 <see cref="GlobalConfigRepository"/> 类的新实例。
    /// </summary>
    /// <param name="dbContext">数据库上下文，提供 SqlSugar 客户端。</param>
    public GlobalConfigRepository(IDbContext dbContext) : base(dbContext) { }

    /// <summary>
    /// 读取全局配置表的首条（单例）记录。
    /// </summary>
    /// <returns>全局配置实体；表为空时返回 null。</returns>
    public async Task<GlobalConfig?> GetFirstAsync()
    {
        // 全局配置表仅一条记录，按主键排序取首条即可
        return await _db.Queryable<GlobalConfig>()
            .OrderBy(g => g.Id)
            .FirstAsync();
    }
}
