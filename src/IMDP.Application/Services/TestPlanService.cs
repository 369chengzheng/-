// =============================================================================
// 文件名：TestPlanService.cs
// 功能描述：检测计划服务实现，统一管理板卡配置与整机配置的增删改查及历史审计记录查询。
//           所有写操作（保存/删除）均同步写入审计日志，保留操作前后的数据快照（JSON），
//           支持 INSERT/UPDATE/DELETE 三类操作的可追溯审计。
//           重构后采用分表仓储：4 个仓储（BoardConfig/WholeConfig/BoardConfigAuditLog/WholeConfigAuditLog）
//           通过 category 参数路由到 {Category}{基表名} 物理表，1 个仓储管理 4 张物理表。
// =============================================================================
using IMDP.Application.DTOs;
using IMDP.Domain.Entities;
using IMDP.Domain.Interfaces;
using IMDP.Domain.Products;
using Newtonsoft.Json;

namespace IMDP.Application.Services;

/// <summary>
/// 检测计划服务实现
/// <para>实现 <see cref="ITestPlanService"/>，通过 4 个分表仓储按产品类别
/// （GasDG/GasOG/WaterDW/WaterOW）路由到对应物理表，避免直接依赖产品仓储工厂。</para>
/// <para>审计策略：保存（UPDATE/INSERT）与删除（DELETE）操作均向对应产品的审计日志表 INSERT 一条记录，
/// 记录操作前（DeletedData）与操作后（InsertedData）的 JSON 快照，确保可追溯。</para>
/// <para>路由策略：板卡配置与整机配置的增删改查通过 category 参数路由到对应物理表。</para>
/// </summary>
public class TestPlanService : ITestPlanService
{
    /// <summary>板卡配置仓储（分表，通过 category 路由到 {Category}BoardConfig 物理表）</summary>
    private readonly IBoardConfigRepository _boardConfigRepo;
    /// <summary>整机配置仓储（分表，通过 category 路由到 {Category}WholeConfig 物理表）</summary>
    private readonly IWholeConfigRepository _wholeConfigRepo;
    /// <summary>板卡配置审计日志仓储（分表，通过 category 路由到 {Category}BoardConfigAuditLog 物理表）</summary>
    private readonly IBoardConfigAuditLogRepository _boardConfigAuditLogRepo;
    /// <summary>整机配置审计日志仓储（分表，通过 category 路由到 {Category}WholeConfigAuditLog 物理表）</summary>
    private readonly IWholeConfigAuditLogRepository _wholeConfigAuditLogRepo;

    /// <summary>
    /// 当前登录工号及角色，新独立方案不使用硬编码账号
    /// </summary>
    private readonly ICurrentContext _currentContext;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="boardConfigRepo">板卡配置仓储</param>
    /// <param name="wholeConfigRepo">整机配置仓储</param>
    /// <param name="boardConfigAuditLogRepo">板卡配置审计日志仓储</param>
    /// <param name="wholeConfigAuditLogRepo">整机配置审计日志仓储</param>
    /// <param name="currentContext">当前登录工号及角色</param>
    public TestPlanService(
        IBoardConfigRepository boardConfigRepo,
        IWholeConfigRepository wholeConfigRepo,
        IBoardConfigAuditLogRepository boardConfigAuditLogRepo,
        IWholeConfigAuditLogRepository wholeConfigAuditLogRepo,
        ICurrentContext currentContext)
    {
        _boardConfigRepo = boardConfigRepo;
        _wholeConfigRepo = wholeConfigRepo;
        _boardConfigAuditLogRepo = boardConfigAuditLogRepo;
        _wholeConfigAuditLogRepo = wholeConfigAuditLogRepo;
        _currentContext = currentContext;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 路由策略：通过 <paramref name="category"/> 路由到对应物理表。
    /// </remarks>
    public async Task<TestPlanDto?> GetBoardConfigAsync(string productCode, string category)
    {
        // 未知类别返回 null（与原 switch/case default 分支行为一致）
        if (!IsKnownCategory(category)) return null;

        var config = await _boardConfigRepo.GetByProductCodeAsync(productCode, category);
        return config == null ? null : MapBoardConfigToDto(config, category);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 路由策略：通过 <paramref name="category"/> 路由到对应物理表。
    /// </remarks>
    public async Task<PagedResultDto<TestPlanDto>> GetBoardConfigsAsync(string productCodeKeyword, int pageIndex, int pageSize, string category)
    {
        // 未知类别返回空结果（与原 switch/case default 分支行为一致）
        if (!IsKnownCategory(category))
            return new PagedResultDto<TestPlanDto> { Items = new List<TestPlanDto>(), TotalCount = 0, PageIndex = pageIndex, PageSize = pageSize };

        var result = await _boardConfigRepo.GetPagedAsync(productCodeKeyword, pageIndex, pageSize, category);
        return new PagedResultDto<TestPlanDto>
        {
            Items = result.Items.Select(c => MapBoardConfigToDto(c, category)).ToList(),
            TotalCount = result.TotalCount,
            PageIndex = result.PageIndex,
            PageSize = result.PageSize
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 路由策略：通过 <paramref name="plan"/> 的 Category 字段路由到对应物理表与审计日志表。
    /// 审计流程（UPDATE 分支）：
    /// 1. 序列化变更前的实体 → deletedJson（旧数据快照）；
    /// 2. 更新实体的 ParametersJson/StationCount/UpdatedBy/UpdatedTime；
    /// 3. UPDATE 配置表（通过 entity.Category 路由）；
    /// 4. 序列化变更后的实体 → insertedJson（新数据快照）；
    /// 5. INSERT 审计日志（OperationType=UPDATE，含旧/新快照）。
    /// 审计流程（INSERT 分支）：
    /// 1. 构造新实体并 INSERT 到配置表；
    /// 2. 序列化新实体 → insertedJson；
    /// 3. INSERT 审计日志（OperationType=INSERT，DeletedData=null）。
    /// 注意：UPDATE 分支查询返回的 existing.Category 为空（IsIgnore），需重新赋值为 category 以路由到正确物理表。
    /// </remarks>
    public async Task<bool> SaveBoardConfigAsync(TestPlanDto plan, string operatorWorkNumber)
    {
        // 未知类别返回 false（与原 switch/case default 分支行为一致）
        if (!IsKnownCategory(plan.Category)) return false;

        var parametersJson = JsonConvert.SerializeObject(plan.Parameters);
        var category = plan.Category;

        var existing = await _boardConfigRepo.GetByProductCodeAsync(plan.ProductCode, category);

        if (existing != null)
        {
            // ===== UPDATE 分支 =====
            // 注意：existing.Category 因 [SugarColumn(IsIgnore = true)] 查询时未被填充，需显式赋值以路由到正确物理表
            existing.Category = category;
            var deletedJson = JsonConvert.SerializeObject(existing);

            existing.ParametersJson = parametersJson;
            existing.StationCount = plan.StationCount;
            existing.UpdatedBy = operatorWorkNumber;
            existing.UpdatedTime = DateTime.Now;
            await _boardConfigRepo.UpdateAsync(existing);

            var insertedJson = JsonConvert.SerializeObject(existing);
            await _boardConfigAuditLogRepo.AddAsync(CreateBoardConfigAuditLog(category, existing.Id, "UPDATE", deletedJson, insertedJson, operatorWorkNumber));
        }
        else
        {
            // ===== INSERT 分支 =====
            var config = CreateBoardConfig(category, plan, parametersJson, operatorWorkNumber);
            await _boardConfigRepo.AddAsync(config);

            var insertedJson = JsonConvert.SerializeObject(config);
            await _boardConfigAuditLogRepo.AddAsync(CreateBoardConfigAuditLog(category, config.Id, "INSERT", null, insertedJson, operatorWorkNumber));
        }
        return true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 路由策略：通过 <paramref name="category"/> 路由到对应物理表与审计日志表。
    /// 审计流程（DELETE）：
    /// 1. 序列化待删除实体 → deletedJson（旧数据快照）；
    /// 2. INSERT 审计日志（OperationType=DELETE，InsertedData=null）；
    /// 3. 软删除配置表记录（IsDeleted=true）。
    /// </remarks>
    public async Task<bool> DeleteBoardConfigAsync(long id, string operatorWorkNumber, string category)
    {
        // 未知类别返回 false（与原 switch/case default 分支行为一致）
        if (!IsKnownCategory(category)) return false;

        var existing = await _boardConfigRepo.GetByIdAsync(id, category);
        if (existing == null) return false;

        var deletedJson = JsonConvert.SerializeObject(existing);
        await _boardConfigAuditLogRepo.AddAsync(CreateBoardConfigAuditLog(category, existing.Id, "DELETE", deletedJson, null, operatorWorkNumber));

        await _boardConfigRepo.DeleteAsync(id, category);
        return true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 路由策略：通过 <paramref name="category"/> 路由到对应物理表。
    /// </remarks>
    public async Task<TestPlanDto?> GetWholeConfigAsync(int planType, string productCode, string customerCode, string category)
    {
        // 未知类别返回 null（与原 switch/case default 分支行为一致）
        if (!IsKnownCategory(category)) return null;

        var config = await _wholeConfigRepo.GetByProductAndCustomerAsync(planType, productCode, customerCode, category);
        return config == null ? null : MapWholeConfigToDto(config, category);
    }

    /// <inheritdoc/>
    public async Task<TestPlanDto?> GetWholeConfigAsync(int planType, string productCode, string customerCode, string category,
        string productKey, string materialNumber)
    {
        category = category.Trim();
        productKey = productKey.Trim();
        materialNumber = materialNumber.Trim();
        productCode = productCode.Trim();
        customerCode = customerCode.Trim();
        var result = await _wholeConfigRepo.GetScopedAsync(planType, productCode, customerCode, category, productKey, materialNumber);
        return result == null ? null : MapWholeConfigToDto(result, category);
    }

    /// <inheritdoc/>
    public async Task<PagedResultDto<TestPlanDto>> GetWholeConfigsAsync(int planType, string productCodeKeyword, string customerCodeKeyword,
        int pageIndex, int pageSize, string category, string productKey, string materialNumber)
    {
        category = category.Trim();
        productKey = productKey.Trim();
        materialNumber = materialNumber.Trim();
        productCodeKeyword = productCodeKeyword?.Trim() ?? string.Empty;
        customerCodeKeyword = customerCodeKeyword?.Trim() ?? string.Empty;
        var result = await _wholeConfigRepo.GetScopedPagedAsync(planType, productCodeKeyword, customerCodeKeyword,
            pageIndex, pageSize, category, productKey, materialNumber);
        return new PagedResultDto<TestPlanDto>
        {
            Items = result.Items.Select(config => MapWholeConfigToDto(config, category)).ToList(),
            TotalCount = result.TotalCount,
            PageIndex = result.PageIndex,
            PageSize = result.PageSize
        };
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteWholeConfigAsync(long id, int planType, string productCode, string customerCode,
        string category, string productKey, string materialNumber) =>
        await _wholeConfigRepo.DeleteScopedAsync(id, planType, productCode.Trim(), customerCode.Trim(),
            category.Trim(), productKey.Trim(), materialNumber.Trim(), _currentContext.UserName);

    /// <inheritdoc/>
    /// <remarks>
    /// 路由策略：通过 <paramref name="category"/> 路由到对应物理表。
    /// </remarks>
    public async Task<PagedResultDto<TestPlanDto>> GetWholeConfigsAsync(int? planType, string productCodeKeyword, string customerCodeKeyword, int pageIndex, int pageSize, string category)
    {
        // 未知类别返回空结果（与原 switch/case default 分支行为一致）
        if (!IsKnownCategory(category))
            return new PagedResultDto<TestPlanDto> { Items = new List<TestPlanDto>(), TotalCount = 0, PageIndex = pageIndex, PageSize = pageSize };

        var singleResult = await _wholeConfigRepo.GetPagedAsync(planType, productCodeKeyword, customerCodeKeyword, pageIndex, pageSize, category);
        return new PagedResultDto<TestPlanDto>
        {
            Items = singleResult.Items.Select(c => MapWholeConfigToDto(c, category)).ToList(),
            TotalCount = singleResult.TotalCount,
            PageIndex = singleResult.PageIndex,
            PageSize = singleResult.PageSize
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 路由策略：通过 <paramref name="plan"/> 的 Category 字段路由到对应物理表与审计日志表。
    /// 审计流程（UPDATE 分支）：
    /// 1. 序列化变更前的实体 → deletedJson（旧数据快照）；
    /// 2. 更新实体的 ParametersJson/StationCount/UpdatedBy/UpdatedTime；
    /// 3. UPDATE 配置表（通过 entity.Category 路由）；
    /// 4. 序列化变更后的实体 → insertedJson（新数据快照）；
    /// 5. INSERT 审计日志（OperationType=UPDATE，含旧/新快照）。
    /// 审计流程（INSERT 分支）：
    /// 1. 构造新实体并 INSERT 到配置表；
    /// 2. 序列化新实体 → insertedJson；
    /// 3. INSERT 审计日志（OperationType=INSERT，DeletedData=null）。
    /// 注意：UPDATE 分支查询返回的 existing.Category 为空（IsIgnore），需重新赋值为 category 以路由到正确物理表。
    /// </remarks>
    public async Task<bool> SaveWholeConfigAsync(TestPlanDto plan, string operatorWorkNumber)
    {
        // 未知类别返回 false（与原 switch/case default 分支行为一致）
        if (!IsKnownCategory(plan.Category)) return false;

        var parametersJson = JsonConvert.SerializeObject(plan.Parameters);
        var category = plan.Category;

        var existing = await _wholeConfigRepo.GetByProductAndCustomerAsync(plan.PlanType, plan.ProductCode, plan.CustomerCode, category);

        if (existing != null)
        {
            // ===== UPDATE 分支 =====
            // 注意：existing.Category 因 [SugarColumn(IsIgnore = true)] 查询时未被填充，需显式赋值以路由到正确物理表
            existing.Category = category;
            var deletedJson = JsonConvert.SerializeObject(existing);

            existing.ParametersJson = parametersJson;
            existing.StationCount = plan.StationCount;
            existing.UpdatedBy = operatorWorkNumber;
            existing.UpdatedTime = DateTime.Now;
            await _wholeConfigRepo.UpdateAsync(existing);

            var insertedJson = JsonConvert.SerializeObject(existing);
            await _wholeConfigAuditLogRepo.AddAsync(CreateWholeConfigAuditLog(category, existing.Id, "UPDATE", deletedJson, insertedJson, operatorWorkNumber));
        }
        else
        {
            // ===== INSERT 分支 =====
            var config = CreateWholeConfig(category, plan, parametersJson, operatorWorkNumber);
            await _wholeConfigRepo.AddAsync(config);

            var insertedJson = JsonConvert.SerializeObject(config);
            await _wholeConfigAuditLogRepo.AddAsync(CreateWholeConfigAuditLog(category, config.Id, "INSERT", null, insertedJson, operatorWorkNumber));
        }
        return true;
    }

    /// <inheritdoc/>
    public async Task<TestPlanDto> SaveWholeConfigAsync(int planType, string productCode, string customerCode, string category,
        string productKey, string materialNumber, TestPlanDto plan)
    {
        var workNumber = _currentContext.UserName;
        category = category.Trim();
        productKey = productKey.Trim();
        materialNumber = materialNumber.Trim();
        productCode = productCode.Trim();
        customerCode = customerCode.Trim();
        if (plan.Category != category || plan.ProductKey != productKey ||
            plan.MaterialNumber != materialNumber || plan.PlanType != planType ||
            plan.ProductCode != productCode || plan.CustomerCode != customerCode)
            throw new ArgumentException("方案与当前物料、类型或产品及客户编码不匹配。");
        if (plan.StationCount < 1 || plan.Parameters == null || !plan.Parameters.Any(item => item.IsEnabled))
            throw new ArgumentException("请提供有效工位数量及至少一个已启用项目。");
        var result = await _wholeConfigRepo.SaveScopedAsync(planType, productCode, customerCode, category, productKey, materialNumber, plan.StationCount,
            JsonConvert.SerializeObject(plan.Parameters), workNumber);
        return MapWholeConfigToDto(result, category);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 路由策略：通过 <paramref name="category"/> 路由到对应物理表与审计日志表。
    /// 审计流程（DELETE）：
    /// 1. 序列化待删除实体 → deletedJson（旧数据快照）；
    /// 2. INSERT 审计日志（OperationType=DELETE，InsertedData=null）；
    /// 3. 软删除配置表记录（IsDeleted=true）。
    /// </remarks>
    public async Task<bool> DeleteWholeConfigAsync(long id, string operatorWorkNumber, string category)
    {
        // 未知类别返回 false（与原 switch/case default 分支行为一致）
        if (!IsKnownCategory(category)) return false;

        var existing = await _wholeConfigRepo.GetByIdAsync(id, category);
        if (existing == null) return false;

        var deletedJson = JsonConvert.SerializeObject(existing);
        await _wholeConfigAuditLogRepo.AddAsync(CreateWholeConfigAuditLog(category, existing.Id, "DELETE", deletedJson, null, operatorWorkNumber));

        await _wholeConfigRepo.DeleteAsync(id, category);
        return true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 路由策略：通过 <paramref name="category"/> 路由到对应审计日志物理表。
    /// </remarks>
    public async Task<PagedResultDto<TestPlanDto>> GetBoardConfigHistoryAsync(string productCodeKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize, string category)
    {
        // 未知类别返回空结果（与原 switch/case default 分支行为一致）
        if (!IsKnownCategory(category))
            return new PagedResultDto<TestPlanDto> { Items = new List<TestPlanDto>(), TotalCount = 0, PageIndex = pageIndex, PageSize = pageSize };

        var result = await _boardConfigAuditLogRepo.GetPagedAsync(productCodeKeyword, startTime, endTime, pageIndex, pageSize, category);

        // 统一用 BoardConfig 类型反序列化（SplitTable 方案，无需按 category 区分实体类型）
        var items = result.Items.Select(a => ParseBoardAuditLog<IBoardConfigAuditLog, BoardConfig>(a, category));

        return new PagedResultDto<TestPlanDto>
        {
            Items = items.ToList(),
            TotalCount = result.TotalCount,
            PageIndex = result.PageIndex,
            PageSize = result.PageSize
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 路由策略：通过 <paramref name="category"/> 路由到对应审计日志物理表。
    /// </remarks>
    public async Task<PagedResultDto<TestPlanDto>> GetWholeConfigHistoryAsync(int? planType, string productCodeKeyword, string customerCodeKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize, string category)
    {
        // 未知类别返回空结果（与原 switch/case default 分支行为一致）
        if (!IsKnownCategory(category))
            return new PagedResultDto<TestPlanDto> { Items = new List<TestPlanDto>(), TotalCount = 0, PageIndex = pageIndex, PageSize = pageSize };

        var singleResult = await _wholeConfigAuditLogRepo.GetPagedAsync(planType, productCodeKeyword, customerCodeKeyword, startTime, endTime, pageIndex, pageSize, category);

        // 统一用 WholeConfig 类型反序列化（SplitTable 方案，无需按 category 区分实体类型）
        var items = singleResult.Items.Select(a => ParseWholeAuditLog<IWholeConfigAuditLog, WholeConfig>(a, category));

        return new PagedResultDto<TestPlanDto>
        {
            Items = items.ToList(),
            TotalCount = singleResult.TotalCount,
            PageIndex = singleResult.PageIndex,
            PageSize = singleResult.PageSize
        };
    }

    // ===== 实体创建辅助方法：构造分表实体并设置 Category 字段以路由到物理表 =====

    /// <summary>
    /// 创建板卡配置实体并设置 Category
    /// <para>赋值逻辑对所有产品相同，Category 字段决定物理表路由。</para>
    /// </summary>
    private static BoardConfig CreateBoardConfig(string category, TestPlanDto plan, string parametersJson, string operatorWorkNumber)
    {
        var now = DateTime.Now;
        return new BoardConfig
        {
            Category = category,
            ProductCode = plan.ProductCode,
            MaterialNumber = plan.MaterialNumber,
            ParametersJson = parametersJson,
            StationCount = plan.StationCount,
            CreatedBy = operatorWorkNumber,
            CreatedTime = now,
            UpdatedBy = operatorWorkNumber,
            UpdatedTime = now
        };
    }

    /// <summary>
    /// 创建整机配置实体并设置 Category
    /// <para>赋值逻辑对所有产品相同，Category 字段决定物理表路由。</para>
    /// </summary>
    private static WholeConfig CreateWholeConfig(string category, TestPlanDto plan, string parametersJson, string operatorWorkNumber)
    {
        var now = DateTime.Now;
        return new WholeConfig
        {
            Category = category,
            PlanType = plan.PlanType,
            ProductCode = plan.ProductCode,
            CustomerCode = plan.CustomerCode,
            MaterialNumber = plan.MaterialNumber,
            ParametersJson = parametersJson,
            StationCount = plan.StationCount,
            CreatedBy = operatorWorkNumber,
            CreatedTime = now,
            UpdatedBy = operatorWorkNumber,
            UpdatedTime = now
        };
    }

    /// <summary>
    /// 创建板卡配置审计日志实体并设置 Category
    /// <para>Category 字段决定物理表路由（{Category}BoardConfigAuditLog）。</para>
    /// </summary>
    private static BoardConfigAuditLog CreateBoardConfigAuditLog(string category, long recordId, string operationType, string? deletedData, string? insertedData, string createdBy)
    {
        var now = DateTime.Now;
        return new BoardConfigAuditLog
        {
            Category = category,
            RecordId = recordId,
            OperationType = operationType,
            DeletedData = deletedData,
            InsertedData = insertedData,
            CreatedBy = createdBy,
            CreatedTime = now
        };
    }

    /// <summary>
    /// 创建整机配置审计日志实体并设置 Category
    /// <para>Category 字段决定物理表路由（{Category}WholeConfigAuditLog）。</para>
    /// </summary>
    private static WholeConfigAuditLog CreateWholeConfigAuditLog(string category, long recordId, string operationType, string? deletedData, string? insertedData, string createdBy)
    {
        var now = DateTime.Now;
        return new WholeConfigAuditLog
        {
            Category = category,
            RecordId = recordId,
            OperationType = operationType,
            DeletedData = deletedData,
            InsertedData = insertedData,
            CreatedBy = createdBy,
            CreatedTime = now
        };
    }

    /// <summary>判断产品类别是否受支持（4 个主类别）</summary>
    private static bool IsKnownCategory(string category)
        => category is ProductCategory.GasDG or ProductCategory.GasOG or ProductCategory.WaterDW or ProductCategory.WaterOW;

    // ===== 通用泛型方法（保留，无需修改）：接收 IBoardConfig / IWholeConfig / IBoardConfigAuditLog / IWholeConfigAuditLog 接口 =====

    /// <summary>
    /// 将板卡配置审计日志解析为检测计划 DTO（通用泛型版本）
    /// <para>DELETE 操作取 DeletedData（旧快照），INSERT/UPDATE 操作取 InsertedData（新快照）。</para>
    /// <para>泛型参数 TAudit 为审计日志实体类型，TConfig 为对应板卡配置实体类型（用于 JSON 反序列化）。</para>
    /// </summary>
    /// <typeparam name="TAudit">板卡配置审计日志实体类型</typeparam>
    /// <typeparam name="TConfig">板卡配置实体类型（必须有无参构造）</typeparam>
    /// <param name="auditLog">板卡配置审计日志实体</param>
    /// <param name="category">产品类别</param>
    /// <returns>检测计划 DTO（OperationType 标识操作类型，UpdatedTime/UpdatedBy 取自审计日志）</returns>
    private static TestPlanDto ParseBoardAuditLog<TAudit, TConfig>(TAudit auditLog, string category)
        where TAudit : IBoardConfigAuditLog
        where TConfig : IBoardConfig, new()
    {
        var json = auditLog.OperationType == "DELETE" ? auditLog.DeletedData : auditLog.InsertedData;
        var config = json != null
            ? JsonConvert.DeserializeObject<TConfig>(json)!
            : new TConfig();

        var dto = MapBoardConfigToDto(config, category);
        dto.OperationType = auditLog.OperationType;
        dto.UpdatedTime = auditLog.CreatedTime;
        dto.UpdatedBy = auditLog.CreatedBy;
        return dto;
    }

    /// <summary>
    /// 将整机配置审计日志解析为检测计划 DTO（通用泛型版本）
    /// <para>DELETE 操作取 DeletedData（旧快照），INSERT/UPDATE 操作取 InsertedData（新快照）。</para>
    /// <para>泛型参数 TAudit 为审计日志实体类型，TConfig 为对应整机配置实体类型（用于 JSON 反序列化）。</para>
    /// </summary>
    /// <typeparam name="TAudit">整机配置审计日志实体类型</typeparam>
    /// <typeparam name="TConfig">整机配置实体类型（必须有无参构造）</typeparam>
    /// <param name="auditLog">整机配置审计日志实体</param>
    /// <param name="category">产品类别</param>
    /// <returns>检测计划 DTO（OperationType 标识操作类型，UpdatedTime/UpdatedBy 取自审计日志）</returns>
    private static TestPlanDto ParseWholeAuditLog<TAudit, TConfig>(TAudit auditLog, string category)
        where TAudit : IWholeConfigAuditLog
        where TConfig : IWholeConfig, new()
    {
        var json = auditLog.OperationType == "DELETE" ? auditLog.DeletedData : auditLog.InsertedData;
        var config = json != null
            ? JsonConvert.DeserializeObject<TConfig>(json)!
            : new TConfig();

        var dto = MapWholeConfigToDto(config, category);
        dto.OperationType = auditLog.OperationType;
        dto.UpdatedTime = auditLog.CreatedTime;
        dto.UpdatedBy = auditLog.CreatedBy;
        return dto;
    }

    /// <summary>
    /// 将板卡配置实体映射为检测计划 DTO（通用版本）
    /// <para>反序列化 ParametersJson 为 <see cref="TestParameterDto"/> 列表，填充 Category 字段。</para>
    /// </summary>
    /// <param name="config">板卡配置实体</param>
    /// <param name="category">产品类别</param>
    /// <returns>检测计划 DTO</returns>
    private static TestPlanDto MapBoardConfigToDto(IBoardConfig config, string category)
    {
        return new TestPlanDto
        {
            Id = config.Id,
            ProductCode = config.ProductCode,
            MaterialNumber = config.MaterialNumber,
            ProductKey = config.ProductKey,
            Category = category,
            StationCount = config.StationCount,
            Parameters = JsonConvert.DeserializeObject<List<TestParameterDto>>(config.ParametersJson) ?? new List<TestParameterDto>(),
            CreatedBy = config.CreatedBy,
            CreatedTime = config.CreatedTime,
            UpdatedBy = config.UpdatedBy,
            UpdatedTime = config.UpdatedTime
        };
    }

    /// <summary>
    /// 将整机配置实体映射为检测计划 DTO（通用版本）
    /// <para>反序列化 ParametersJson 为 <see cref="TestParameterDto"/> 列表，填充 Category 字段。</para>
    /// </summary>
    /// <param name="config">整机配置实体</param>
    /// <param name="category">产品类别</param>
    /// <returns>检测计划 DTO</returns>
    private static TestPlanDto MapWholeConfigToDto(IWholeConfig config, string category)
    {
        return new TestPlanDto
        {
            Id = config.Id,
            PlanType = config.PlanType,
            ProductCode = config.ProductCode,
            CustomerCode = config.CustomerCode,
            MaterialNumber = config.MaterialNumber,
            ProductKey = config.ProductKey,
            Category = category,
            StationCount = config.StationCount,
            Parameters = JsonConvert.DeserializeObject<List<TestParameterDto>>(config.ParametersJson) ?? new List<TestParameterDto>(),
            CreatedBy = config.CreatedBy,
            CreatedTime = config.CreatedTime,
            UpdatedBy = config.UpdatedBy,
            UpdatedTime = config.UpdatedTime
        };
    }
}
