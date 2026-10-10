// =============================================================================
// 文件名：TestRecordService.cs
// 功能描述：检测记录服务实现，提供板卡检测记录与整机检测记录的查询与保存。
//           重构后采用分表仓储：2 个仓储（BoardRecord/WholeRecord）通过 category 参数路由到
//           {Category}Boards / {Category}Wholes 物理表，1 个仓储管理 4 张物理表。
//           查询支持多条件组合筛选；保存时将 DTO 映射为分表实体并设置 Category 后 INSERT。
// =============================================================================
using IMDP.Application.DTOs;
using IMDP.Domain.Entities;
using IMDP.Domain.Interfaces;
using IMDP.Domain.Products;

namespace IMDP.Application.Services;

/// <summary>
/// 检测记录服务实现
/// <para>实现 <see cref="ITestRecordService"/>，通过 2 个分表仓储按产品类别
/// （GasDG/GasOG/WaterDW/WaterOW）路由到对应物理表。</para>
/// <para>查询时将实体映射为 <see cref="TestRecordDto"/>；保存时将 DTO 映射为分表实体并设置 Category 后 INSERT。</para>
/// <para>路由策略：板卡检测记录与整机检测记录均通过 category 参数路由到对应物理表。</para>
/// </summary>
public class TestRecordService : ITestRecordService
{
    /// <summary>板卡检测记录仓储（分表，通过 category 路由到 {Category}Boards 物理表）</summary>
    private readonly IBoardRecordRepository _boardRecordRepo;
    /// <summary>整机检测记录仓储（分表，通过 category 路由到 {Category}Wholes 物理表）</summary>
    private readonly IWholeRecordRepository _wholeRecordRepo;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="boardRecordRepo">板卡检测记录仓储</param>
    /// <param name="wholeRecordRepo">整机检测记录仓储</param>
    public TestRecordService(
        IBoardRecordRepository boardRecordRepo,
        IWholeRecordRepository wholeRecordRepo)
    {
        _boardRecordRepo = boardRecordRepo;
        _wholeRecordRepo = wholeRecordRepo;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 路由策略：通过 <paramref name="category"/> 路由到对应物理表。
    /// </remarks>
    public async Task<PagedResultDto<TestRecordDto>> GetBoardRecordsAsync(string orderNumberKeyword, string productSnKeyword, string workNumberKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize, string category)
    {
        // 未知类别返回空结果（与原 switch/case default 分支行为一致）
        if (!IsKnownCategory(category))
            return new PagedResultDto<TestRecordDto> { Items = new List<TestRecordDto>(), TotalCount = 0, PageIndex = pageIndex, PageSize = pageSize };

        var result = await _boardRecordRepo.GetPagedAsync(orderNumberKeyword, productSnKeyword, workNumberKeyword, startTime, endTime, pageIndex, pageSize, category);
        return ToDtoResult(result, x => MapToDto(x, category));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 路由策略：通过 <paramref name="category"/> 路由到对应物理表。
    /// </remarks>
    public async Task<PagedResultDto<TestRecordDto>> GetWholeRecordsAsync(int? testType, string orderNumberKeyword, string productSnKeyword, string workNumberKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize, string category)
    {
        // 未知类别返回空结果（与原 switch/case default 分支行为一致）
        if (!IsKnownCategory(category))
            return new PagedResultDto<TestRecordDto> { Items = new List<TestRecordDto>(), TotalCount = 0, PageIndex = pageIndex, PageSize = pageSize };

        var singleResult = await _wholeRecordRepo.GetPagedAsync(testType, orderNumberKeyword, productSnKeyword, workNumberKeyword, startTime, endTime, pageIndex, pageSize, category);
        return ToDtoResult(singleResult, x => MapToDto(x, category));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 路由策略：通过 <paramref name="record"/> 的 Category 字段路由到对应物理表。
    /// 实体 Category 字段决定物理表路由（{Category}Boards），由 AddAsync 内部 AS 调用使用。
    /// </remarks>
    /// <exception cref="ArgumentException">当 <paramref name="record"/>.Category 不是受支持的产品类别时抛出</exception>
    public async Task<long> SaveBoardRecordAsync(TestRecordDto record)
    {
        // 未知类别抛异常（与原 switch/case default 分支行为一致）
        if (!IsKnownCategory(record.Category))
            throw new ArgumentException($"未知产品类别: {record.Category}", nameof(record));

        var entity = CreateBoardRecord(record);
        var result = await _boardRecordRepo.AddAsync(entity);
        return result.Id;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 路由策略：通过 <paramref name="record"/> 的 Category 字段路由到对应物理表。
    /// 实体 Category 字段决定物理表路由（{Category}Wholes），由 AddAsync 内部 AS 调用使用。
    /// </remarks>
    /// <exception cref="ArgumentException">当 <paramref name="record"/>.Category 不是受支持的产品类别时抛出</exception>
    public async Task<long> SaveWholeRecordAsync(TestRecordDto record)
    {
        // 未知类别抛异常（与原 switch/case default 分支行为一致）
        if (!IsKnownCategory(record.Category))
            throw new ArgumentException($"未知产品类别: {record.Category}", nameof(record));

        var entity = CreateWholeRecord(record);
        var result = await _wholeRecordRepo.AddAsync(entity);
        return result.Id;
    }

    // ===== 实体创建辅助方法：构造分表实体并设置 Category 字段以路由到物理表 =====

    /// <summary>
    /// 创建板卡检测记录实体并设置 Category
    /// <para>赋值逻辑对所有产品相同，Category 字段决定物理表路由（{Category}Boards）。</para>
    /// </summary>
    private static Boards CreateBoardRecord(TestRecordDto record)
    {
        var now = DateTime.Now;
        return new Boards
        {
            Category = record.Category,
            OrderNumber = record.OrderNumber,
            ProductSn = record.ProductSn,
            ProductCode = record.ProductCode,
            MaterialNumber = record.MaterialNumber,
            TestContentJson = record.TestContentJson,
            TestResult = record.TestResult,
            CreatedBy = record.CreatedBy,
            CreatedTime = now
        };
    }

    /// <summary>
    /// 创建整机检测记录实体并设置 Category
    /// <para>赋值逻辑对所有产品相同，Category 字段决定物理表路由（{Category}Wholes）。</para>
    /// </summary>
    private static Wholes CreateWholeRecord(TestRecordDto record)
    {
        var now = DateTime.Now;
        return new Wholes
        {
            Category = record.Category,
            TestType = record.TestType,
            OrderNumber = record.OrderNumber,
            ProductSn = record.ProductSn,
            ProductCode = record.ProductCode,
            CustomerCode = record.CustomerCode,
            MaterialNumber = record.MaterialNumber,
            TestContentJson = record.TestContentJson,
            TestResult = record.TestResult,
            CreatedBy = record.CreatedBy,
            CreatedTime = now
        };
    }

    /// <summary>判断产品类别是否受支持（4 个主类别）</summary>
    private static bool IsKnownCategory(string category)
        => category is ProductCategory.GasDG or ProductCategory.GasOG or ProductCategory.WaterDW or ProductCategory.WaterOW;

    // ===== 通用映射方法（保留，无需修改） =====

    /// <summary>
    /// 将整机检测记录实体映射为 DTO（通用版本）
    /// </summary>
    /// <param name="w">整机检测记录实体</param>
    /// <param name="category">产品类别</param>
    /// <returns>检测记录 DTO</returns>
    private static TestRecordDto MapToDto(IWholes w, string category)
    {
        return new TestRecordDto
        {
            Id = w.Id,
            TestType = w.TestType,
            OrderNumber = w.OrderNumber,
            ProductSn = w.ProductSn,
            ProductCode = w.ProductCode,
            CustomerCode = w.CustomerCode,
            MaterialNumber = w.MaterialNumber,
            Category = category,
            TestResult = w.TestResult,
            TestContentJson = w.TestContentJson,
            CreatedBy = w.CreatedBy,
            CreatedTime = w.CreatedTime
        };
    }

    /// <summary>
    /// 将检测记录分页结果映射为 DTO 分页结果（通用泛型版本）
    /// <para>通过 mapper 委托将每个实体映射为 DTO，板卡与整机通用。</para>
    /// </summary>
    /// <typeparam name="T">检测记录实体类型</typeparam>
    /// <param name="result">实体分页结果</param>
    /// <param name="mapper">实体到 DTO 的映射委托</param>
    /// <returns>DTO 分页结果</returns>
    private static PagedResultDto<TestRecordDto> ToDtoResult<T>(PagedResult<T> result, Func<T, TestRecordDto> mapper)
    {
        return new PagedResultDto<TestRecordDto>
        {
            Items = result.Items.Select(mapper).ToList(),
            TotalCount = result.TotalCount,
            PageIndex = result.PageIndex,
            PageSize = result.PageSize
        };
    }

    /// <summary>
    /// 将板卡检测记录实体映射为 DTO（通用版本）
    /// </summary>
    /// <param name="b">板卡检测记录实体</param>
    /// <param name="category">产品类别</param>
    /// <returns>检测记录 DTO</returns>
    private static TestRecordDto MapToDto(IBoards b, string category)
    {
        return new TestRecordDto
        {
            Id = b.Id,
            OrderNumber = b.OrderNumber,
            ProductSn = b.ProductSn,
            ProductCode = b.ProductCode,
            MaterialNumber = b.MaterialNumber,
            Category = category,
            TestResult = b.TestResult,
            TestContentJson = b.TestContentJson,
            CreatedBy = b.CreatedBy,
            CreatedTime = b.CreatedTime
        };
    }
}
