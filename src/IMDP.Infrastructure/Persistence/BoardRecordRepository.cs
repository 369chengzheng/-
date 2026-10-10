// =============================================================================
// 文件名：BoardRecordRepository.cs
// 功能描述：板卡检测记录仓储实现（分表合并版）
//           继承 SplitTableRepository<Boards>，通过 AS(tableName) 动态路由到
//           {Category}Boards 物理表（GasDGBoards/GasOGBoards/WaterDWBoards/WaterOWBoards）。
//           提供板卡生产记录的多条件分页查询；重写 AddAsync 通过 AS 指定物理表插入数据。
// =============================================================================
using IMDP.Domain.Entities;
using IMDP.Domain.Interfaces;
using SqlSugar;

namespace IMDP.Infrastructure.Persistence;

/// <summary>
/// 板卡检测记录仓储实现（分表合并版）
/// <para>继承 <see cref="SplitTableRepository{T}"/>，通过 QueryByCategory(category) 路由到对应物理表。</para>
/// <para>插入使用 AS(GetTableName(entity.Category)) 指定物理表。</para>
/// </summary>
public class BoardRecordRepository : SplitTableRepository<Boards>, IBoardRecordRepository
{
    /// <summary>
    /// 初始化 <see cref="BoardRecordRepository"/> 类的新实例。
    /// </summary>
    /// <param name="dbContext">数据库上下文，提供 SqlSugar 客户端</param>
    public BoardRecordRepository(IDbContext dbContext) : base(dbContext) { }

    /// <inheritdoc/>
    protected override string BaseTableName => "Boards";

    /// <summary>
    /// 按多条件分页查询板卡生产记录。
    /// </summary>
    /// <param name="orderNumberKeyword">订单号关键字（可为空）</param>
    /// <param name="productSnKeyword">产品序列号关键字（可为空）</param>
    /// <param name="workNumberKeyword">工号关键字（可为空，匹配 CreatedBy 字段）</param>
    /// <param name="startTime">创建时间起始值（可为空）</param>
    /// <param name="endTime">创建时间结束值（可为空）</param>
    /// <param name="pageIndex">页索引（从 1 开始）</param>
    /// <param name="pageSize">每页记录数</param>
    /// <param name="category">产品类别</param>
    /// <returns>包含分页数据及总记录数的分页结果</returns>
    public async Task<PagedResult<Boards>> GetPagedAsync(string orderNumberKeyword, string productSnKeyword, string workNumberKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize, string category)
    {
        // SqlSugar 用于承接总记录数的引用参数
        RefAsync<int> totalCount = 0;
        var query = QueryByCategory(category);

        // 按订单号模糊匹配
        if (!string.IsNullOrWhiteSpace(orderNumberKeyword))
            query = query.Where(b => b.OrderNumber.Contains(orderNumberKeyword));

        // 按产品序列号模糊匹配
        if (!string.IsNullOrWhiteSpace(productSnKeyword))
            query = query.Where(b => b.ProductSn.Contains(productSnKeyword));

        // 按 CreationBy（工号）模糊匹配
        if (!string.IsNullOrWhiteSpace(workNumberKeyword))
            query = query.Where(b => b.CreatedBy.Contains(workNumberKeyword));

        // 创建时间范围过滤
        if (startTime.HasValue)
            query = query.Where(b => b.CreatedTime >= startTime.Value);

        if (endTime.HasValue)
            query = query.Where(b => b.CreatedTime <= endTime.Value);

        // 按创建时间倒序分页
        var items = await query.OrderByDescending(b => b.CreatedTime)
            .ToPageListAsync(pageIndex, pageSize, totalCount);

        return new PagedResult<Boards>
        {
            Items = items,
            TotalCount = totalCount.Value,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// 重写新增方法：通过 AS 指定物理表名插入数据。
    /// </summary>
    /// <param name="entity">待新增的板卡检测记录实体（需设置 Category 字段）</param>
    /// <returns>新增后的实体</returns>
    public override async Task<Boards> AddAsync(Boards entity)
    {
        await _db.Insertable(entity).AS(GetTableName(entity.Category)).ExecuteCommandAsync();
        return entity;
    }
}
