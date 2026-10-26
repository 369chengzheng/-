// =============================================================================
// 文件名：WholeConfigRepository.cs
// 功能描述：整机检测配置仓储实现（分表合并版）
//           继承 SplitTableRepository<WholeConfig>，通过 AS(tableName) 动态路由到
//           {Category}WholeConfig 物理表（GasDGWholeConfig 等）。
//           提供按计划类型+产品编码+客户编码查询、分页查询、唯一性校验、按 Id 查询与软删除等功能。
//           重写 AddAsync/UpdateAsync 使用 AS 指定物理表；DeleteAsync(id) 抛异常以强制使用带 category 的重载。
// =============================================================================
using IMDP.Domain.Entities;
using IMDP.Domain.Interfaces;
using SqlSugar;
using IMDP.Domain.Products;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;

namespace IMDP.Infrastructure.Persistence;

/// <summary>
/// 整机检测配置仓储实现（分表合并版）
/// <para>继承 <see cref="SplitTableRepository{T}"/>，通过 QueryByCategory(category) 路由到对应物理表。</para>
/// <para>插入/更新使用 AS(GetTableName(entity.Category)) 指定物理表。</para>
/// </summary>
public class WholeConfigRepository : SplitTableRepository<WholeConfig>, IWholeConfigRepository
{
    /// <summary>
    /// 过滤索引写入所需的会话选项，与事务使用同一条连接
    /// </summary>
    internal const string ScopedWriteOptions = "SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; " +
        "SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET QUOTED_IDENTIFIER ON; SET NUMERIC_ROUNDABORT OFF;";

    /// <summary>
    /// 初始化 <see cref="WholeConfigRepository"/> 类的新实例。
    /// </summary>
    /// <param name="dbContext">数据库上下文，提供 SqlSugar 客户端</param>
    public WholeConfigRepository(IDbContext dbContext) : base(dbContext) { }

    /// <inheritdoc/>
    protected override string BaseTableName => "WholeConfig";

    /// <summary>
    /// 按主键 Id + 产品类别查询单条整机配置（仅返回未软删除的记录）。
    /// </summary>
    /// <param name="id">主键 Id</param>
    /// <param name="category">产品类别</param>
    /// <returns>匹配的整机配置实体；未找到时返回 null</returns>
    public async Task<WholeConfig?> GetByIdAsync(long id, string category)
    {
        return await QueryByCategory(category)
            .Where(w => w.Id == id && !w.IsDeleted)
            .FirstAsync();
    }

    /// <summary>
    /// 按计划类型、产品编码、客户编码联合查询未删除的整机配置。
    /// </summary>
    /// <param name="planType">计划类型</param>
    /// <param name="productCode">产品编码</param>
    /// <param name="customerCode">客户编码</param>
    /// <param name="category">产品类别</param>
    /// <returns>匹配的整机配置实体；未找到时返回 null</returns>
    public async Task<WholeConfig?> GetByProductAndCustomerAsync(int planType, string productCode, string customerCode, string category)
    {
        return await QueryByCategory(category)
            .Where(w => w.PlanType == planType && w.ProductCode == productCode && w.CustomerCode == customerCode && !w.IsDeleted)
            .FirstAsync();
    }

    /// <summary>
    /// 按计划类型、产品编码、客户编码关键字分页查询未删除的整机配置。
    /// </summary>
    /// <param name="planType">计划类型（可为空，空表示不筛选）</param>
    /// <param name="productCodeKeyword">产品编码关键字（可为空）</param>
    /// <param name="customerCodeKeyword">客户编码关键字（可为空）</param>
    /// <param name="pageIndex">页索引（从 1 开始）</param>
    /// <param name="pageSize">每页记录数</param>
    /// <param name="category">产品类别</param>
    /// <returns>包含分页数据及总记录数的分页结果</returns>
    public async Task<PagedResult<WholeConfig>> GetPagedAsync(int? planType, string productCodeKeyword, string customerCodeKeyword, int pageIndex, int pageSize, string category)
    {
        // SqlSugar 用于承接总记录数的引用参数
        RefAsync<int> totalCount = 0;
        // 默认过滤已软删除的记录
        var query = QueryByCategory(category).Where(w => !w.IsDeleted);

        // 按计划类型精确匹配
        if (planType.HasValue)
            query = query.Where(w => w.PlanType == planType.Value);

        // 按产品编码模糊匹配
        if (!string.IsNullOrWhiteSpace(productCodeKeyword))
            query = query.Where(w => w.ProductCode.Contains(productCodeKeyword));

        // 按客户编码模糊匹配
        if (!string.IsNullOrWhiteSpace(customerCodeKeyword))
            query = query.Where(w => w.CustomerCode.Contains(customerCodeKeyword));

        // 按更新时间倒序分页
        var items = await query.OrderByDescending(w => w.UpdatedTime)
            .ToPageListAsync(pageIndex, pageSize, totalCount);

        return new PagedResult<WholeConfig>
        {
            Items = items,
            TotalCount = totalCount.Value,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// 检查指定计划类型下产品编码+客户编码组合是否已存在（用于唯一性校验）。
    /// </summary>
    /// <param name="planType">计划类型</param>
    /// <param name="productCode">产品编码</param>
    /// <param name="customerCode">客户编码</param>
    /// <param name="excludeId">需要排除的实体 Id（更新时用于排除自身）</param>
    /// <param name="category">产品类别</param>
    /// <returns>若已存在返回 true；否则返回 false</returns>
    public async Task<bool> ProductCustomerExistsAsync(int planType, string productCode, string customerCode, long? excludeId, string category)
    {
        var query = QueryByCategory(category)
            .Where(w => w.PlanType == planType && w.ProductCode == productCode && w.CustomerCode == customerCode && !w.IsDeleted);

        // 更新场景下排除当前实体自身
        if (excludeId.HasValue)
            query = query.Where(w => w.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    /// <summary>
    /// 重写新增方法：通过 AS 指定物理表名插入数据。
    /// </summary>
    /// <param name="entity">待新增的整机配置实体（需设置 Category 字段）</param>
    /// <returns>新增后的实体</returns>
    public override async Task<WholeConfig> AddAsync(WholeConfig entity)
    {
        await ExecuteWriteAsync(entity.Category, db => db.Insertable(entity).AS(GetTableName(entity.Category)).ExecuteCommandAsync());
        return entity;
    }

    /// <summary>
    /// 重写更新方法：通过 AS 指定物理表名更新数据。
    /// </summary>
    /// <param name="entity">待更新的整机配置实体（需设置 Category 字段）</param>
    public override async Task UpdateAsync(WholeConfig entity)
    {
        await ExecuteWriteAsync(entity.Category, db => db.Updateable(entity)
            .AS(GetTableName(entity.Category))
            .Where(row => row.Id == entity.Id)
            .ExecuteCommandAsync());
    }

    /// <summary>
    /// 重写按 Id 删除方法：分表场景下无法仅凭 Id 定位物理表，抛异常强制使用带 category 的重载。
    /// </summary>
    /// <param name="id">待删除实体的 Id</param>
    /// <exception cref="NotSupportedException">始终抛出，提示使用 DeleteAsync(long, string) 重载</exception>
    public override async Task DeleteAsync(long id)
    {
        await Task.CompletedTask;
        throw new NotSupportedException("分表场景下请使用 DeleteAsync(long id, string category) 方法");
    }

    /// <summary>
    /// 按主键 Id + 产品类别软删除整机配置（IsDeleted = true）。
    /// </summary>
    /// <param name="id">待删除实体的 Id</param>
    /// <param name="category">产品类别</param>
    public async Task DeleteAsync(long id, string category)
    {
        var entity = await QueryByCategory(category).Where(w => w.Id == id).FirstAsync();
        if (entity != null)
        {
            // 软删除：仅标记删除状态，不物理删除
            entity.IsDeleted = true;
            await ExecuteWriteAsync(category, db => db.Updateable(entity)
                .AS(GetTableName(category))
                .Where(row => row.Id == entity.Id)
                .ExecuteCommandAsync());
        }
    }


    /// <summary>
    /// 国内燃气共表写入时在同一连接应用过滤索引选项，其他分类沿用公共客户端
    /// </summary>
    private async Task ExecuteWriteAsync(string category, Func<ISqlSugarClient, Task<int>> write)
    {
        if (category != ProductCategory.GasDG) { await write(_db); return; }
        using var db = _db.CopyNew();
        await db.Ado.BeginTranAsync();
        try
        {
            await db.Ado.ExecuteCommandAsync(ScopedWriteOptions);
            await write(db);
            await db.Ado.CommitTranAsync();
        }
        catch { await db.Ado.RollbackTranAsync(); throw; }
    }

    /// <inheritdoc/>
    public async Task<WholeConfig?> GetScopedAsync(int planType, string productCode, string customerCode, string category,
        string productKey, string materialNumber)
    {
        CheckScopedCategory(category, productKey);
        using var db = _db.CopyNew();
        await CheckBindingAsync(db, materialNumber, productKey, false);
        var rows = await ScopedQuery(db, planType, productCode, customerCode, category, productKey, materialNumber).Take(2).ToListAsync();
        if (rows.Count > 1) throw new InvalidOperationException("该范围存在重复有效方案，请先核对数据。");
        return rows.SingleOrDefault();
    }

    /// <inheritdoc/>
    public async Task<PagedResult<WholeConfig>> GetScopedPagedAsync(int planType, string productCodeKeyword, string customerCodeKeyword,
        int pageIndex, int pageSize, string category, string productKey, string materialNumber)
    {
        CheckScopedCategory(category, productKey);
        using var db = _db.CopyNew();
        await CheckBindingAsync(db, materialNumber, productKey, false);
        RefAsync<int> totalCount = 0;
        var items = await ScopedListQuery(db, planType, productCodeKeyword, customerCodeKeyword, category, productKey, materialNumber)
            .ToPageListAsync(pageIndex, pageSize, totalCount);
        return new PagedResult<WholeConfig>
        {
            Items = items,
            TotalCount = totalCount.Value,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteScopedAsync(long id, int planType, string productCode, string customerCode,
        string category, string productKey, string materialNumber, string operatorWorkNumber)
    {
        CheckScopedCategory(category, productKey);
        if (string.IsNullOrWhiteSpace(operatorWorkNumber)) throw new ArgumentException("操作员工号无效。");
        using var db = _db.CopyNew();
        await db.Ado.BeginTranAsync(IsolationLevel.Serializable);
        try
        {
            await db.Ado.ExecuteCommandAsync(ScopedWriteOptions);
            await CheckBindingAsync(db, materialNumber, productKey, true);
            var config = await ScopedQuery(db, planType, productCode, customerCode, category, productKey, materialNumber)
                .Where(row => row.Id == id).FirstAsync();
            if (config == null)
            {
                await db.Ado.CommitTranAsync();
                return false;
            }
            var before = AuditSnapshot(config);
            var now = DateTime.Now;
            config.IsDeleted = true;
            config.UpdatedBy = operatorWorkNumber;
            config.UpdatedTime = now;
            if (await ScopedDeleteUpdate(db, config, category).ExecuteCommandAsync() != 1)
                throw new InvalidOperationException("原方案已变化，请重新查询后删除。");
            await db.Insertable(new WholeConfigAuditLog
            {
                RecordId = config.Id,
                OperationType = "DELETE",
                DeletedData = before,
                InsertedData = null,
                CreatedBy = operatorWorkNumber,
                CreatedTime = now,
                ComputerName = config.ComputerName,
                UserName = config.UserName,
                AppName = config.AppName
            }).AS(category + "WholeConfigAuditLog").ExecuteCommandAsync();
            await db.Ado.CommitTranAsync();
            return true;
        }
        catch
        {
            await db.Ado.RollbackTranAsync();
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<WholeConfig> SaveScopedAsync(int planType, string productCode, string customerCode, string category,
        string productKey, string materialNumber, int stationCount, string parametersJson,
        string operatorWorkNumber)
    {
        CheckScopedCategory(category, productKey);
        if (stationCount < 1 || string.IsNullOrWhiteSpace(operatorWorkNumber))
            throw new ArgumentException("工位数量或操作员工号无效。");
        using var db = _db.CopyNew();
        await db.Ado.BeginTranAsync(IsolationLevel.Serializable);
        try
        {
            await db.Ado.ExecuteCommandAsync(ScopedWriteOptions);
            // 同一物料的有效绑定在保存期间保持稳定，并串行化该物料的新增判定。
            await CheckBindingAsync(db, materialNumber, productKey, true);
            var rows = await ScopedQuery(db, planType, productCode, customerCode, category, productKey, materialNumber).Take(2).ToListAsync();
            if (rows.Count > 1) throw new InvalidOperationException("该范围存在重复有效方案，请先核对数据。");
            var existing = rows.SingleOrDefault();
            if (existing != null && SameContent(existing, stationCount, parametersJson))
            {
                await db.Ado.CommitTranAsync();
                return existing;
            }

            var before = existing == null ? null : AuditSnapshot(existing);
            var now = DateTime.Now;
            var config = existing ?? new WholeConfig
            {
                ProductKey = productKey,
                MaterialNumber = materialNumber,
                PlanType = planType,
                ProductCode = productCode,
                CustomerCode = customerCode,
                CreatedBy = operatorWorkNumber,
                CreatedTime = now
            };
            config.ParametersJson = parametersJson;
            config.StationCount = stationCount;
            config.UpdatedBy = operatorWorkNumber;
            config.UpdatedTime = now;
            if (existing == null)
                config.Id = await db.Insertable(config).AS(GetTableName(category)).ExecuteReturnBigIdentityAsync();
            else
            {
                var affected = await db.Updateable(config).AS(GetTableName(category))
                    .Where(row => row.Id == config.Id && row.ProductKey == productKey &&
                        row.MaterialNumber == materialNumber && row.PlanType == planType &&
                        row.ProductCode == productCode && row.CustomerCode == customerCode && !row.IsDeleted)
                    .ExecuteCommandAsync();
                if (affected != 1) throw new InvalidOperationException("原方案已变化，请重新查询后维护。");
            }

            // 重新读取实际保存的字段，用于页面回显及审计快照。
            var saved = await ScopedQuery(db, planType, productCode, customerCode, category, productKey, materialNumber).SingleAsync();
            await db.Insertable(new WholeConfigAuditLog
            {
                RecordId = saved.Id,
                OperationType = existing == null ? "INSERT" : "UPDATE",
                DeletedData = before,
                InsertedData = AuditSnapshot(saved),
                CreatedBy = operatorWorkNumber,
                CreatedTime = now,
                ComputerName = saved.ComputerName,
                UserName = saved.UserName,
                AppName = saved.AppName
            }).AS(category + "WholeConfigAuditLog").ExecuteCommandAsync();
            await db.Ado.CommitTranAsync();
            return saved;
        }
        catch
        {
            await db.Ado.RollbackTranAsync();
            throw;
        }
    }

    /// <summary>
    /// 支持四类整机方案存储，插件标识须与所属产品类别一致
    /// </summary>
    private static void CheckScopedCategory(string category, string productKey)
    {
        if (category is not (ProductCategory.GasDG or ProductCategory.GasOG or ProductCategory.WaterDW or ProductCategory.WaterOW)
            || string.IsNullOrWhiteSpace(productKey)
            || !productKey.StartsWith(category + ".", StringComparison.Ordinal))
            throw new ArgumentException("产品类别或插件标识不匹配。");
    }

    /// <summary>
    /// 软删除仍核对主键和完整范围，只更新未删除记录
    /// </summary>
    private IUpdateable<WholeConfig> ScopedDeleteUpdate(ISqlSugarClient db, WholeConfig config, string category) =>
        db.Updateable(config).AS(GetTableName(category)).Where(row => row.Id == config.Id && row.ProductKey == config.ProductKey &&
            row.MaterialNumber == config.MaterialNumber && row.PlanType == config.PlanType &&
            row.ProductCode == config.ProductCode && row.CustomerCode == config.CustomerCode && !row.IsDeleted);

    /// <summary>
    /// 列表查询固定插件、物料及类型，空编码不筛选，非空编码沿管理页约定模糊匹配
    /// </summary>
    private ISugarQueryable<WholeConfig> ScopedListQuery(ISqlSugarClient db, int planType, string productCodeKeyword, string customerCodeKeyword,
        string category, string productKey, string materialNumber) =>
        db.Queryable<WholeConfig>().AS(GetTableName(category)).Where(row =>
            row.ProductKey == productKey && row.MaterialNumber == materialNumber && row.PlanType == planType && !row.IsDeleted)
            .WhereIF(!string.IsNullOrWhiteSpace(productCodeKeyword), row => row.ProductCode.Contains(productCodeKeyword))
            .WhereIF(!string.IsNullOrWhiteSpace(customerCodeKeyword), row => row.CustomerCode.Contains(customerCodeKeyword))
            .OrderByDescending(row => row.UpdatedTime).OrderByDescending(row => row.Id);

    /// <summary>
    /// 完整范围必须同时出现在查询条件中，不能仅凭产品和客户编码取第一条
    /// </summary>
    private ISugarQueryable<WholeConfig> ScopedQuery(ISqlSugarClient db, int planType, string productCode, string customerCode, string category,
        string productKey, string materialNumber) =>
        db.Queryable<WholeConfig>().AS(GetTableName(category)).Where(row =>
            row.ProductKey == productKey && row.MaterialNumber == materialNumber && row.PlanType == planType &&
            row.ProductCode == productCode && row.CustomerCode == customerCode && !row.IsDeleted);

    /// <summary>
    /// 核对物料仍绑定当前插件，保存时锁定该物料的有效绑定
    /// </summary>
    private static async Task CheckBindingAsync(ISqlSugarClient db, string materialNumber, string productKey, bool locked)
    {
        var query = db.Queryable<MaterialProductMap>().Where(row => row.MaterialNumber == materialNumber && !row.IsDeleted);
        if (locked) query = query.With(SqlWith.UpdLock);
        var maps = await query.Take(2).ToListAsync();
        if (maps.Count != 1) throw new InvalidOperationException("物料有效绑定不存在或不唯一，请返回首页重新确认。");
        List<string>? keys;
        try { keys = JsonConvert.DeserializeObject<List<string>>(maps[0].ProductPluginsJson); }
        catch (JsonException) { throw new InvalidOperationException("物料插件绑定内容无效，请先维护绑定。"); }
        if (keys == null || !keys.Contains(productKey, StringComparer.Ordinal))
            throw new InvalidOperationException("当前物料未绑定该插件，请返回首页重新确认。");
    }

    /// <summary>
    /// 比较实际业务内容，忽略 JSON 空白和对象属性顺序，数组项目顺序仍保留
    /// </summary>
    internal static bool SameContent(WholeConfig config, int stationCount, string parametersJson)
    {
        if (config.StationCount != stationCount) return false;
        var incoming = JToken.Parse(parametersJson);
        try { return JToken.DeepEquals(JToken.Parse(config.ParametersJson), incoming); }
        catch (JsonException) { return false; }
    }

    /// <summary>
    /// 审计沿用原整机快照格式，参数JSON及密码值按用户要求保留原文
    /// 外层快照序列化字符串，不因参数JSON损坏而阻断修复或软删除
    /// </summary>
    internal static string AuditSnapshot(WholeConfig config)
    {
        return JObject.FromObject(config).ToString(Formatting.None);
    }

}
