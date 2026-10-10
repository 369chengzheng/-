// =============================================================================
// 文件名：IGlobalConfigRepository.cs
// 功能描述：全局配置仓储接口，定义读取单例全局配置的契约。
// =============================================================================
using IMDP.Domain.Entities;

namespace IMDP.Domain.Interfaces;

/// <summary>
/// 全局配置仓储接口
/// <para>继承 <see cref="IRepository{T}"/>，扩展读取单例全局配置的方法。</para>
/// </summary>
public interface IGlobalConfigRepository : IRepository<GlobalConfig>
{
    /// <summary>读取全局配置表的首条（单例）记录</summary>
    /// <returns>全局配置实体；表为空时返回 null</returns>
    Task<GlobalConfig?> GetFirstAsync();
}
