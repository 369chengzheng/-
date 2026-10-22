// =============================================================================
// 文件名：IWholeConfigRepository.cs
// 功能描述：整机检测配置仓储接口（分表合并版）。
//           通过 category 参数路由到 {Category}WholeConfig 物理表，
//           1 个仓储管理 4 张物理表（GasDGWholeConfig 等）。
//           继承 IRepository<WholeConfig> 获得标准 CRUD 契约；
//           注意：继承的 GetByIdAsync(long)/DeleteAsync(long) 因无法定位物理表而不可用，
//           请使用本接口新增的带 category 参数的重载。
// =============================================================================
using IMDP.Domain.Entities;

namespace IMDP.Domain.Interfaces;

/// <summary>
/// 整机检测配置仓储接口（分表合并版）
/// <para>继承 <see cref="IRepository{T}"/>，扩展按计划类型+产品编码+客户编码组合查询、唯一性校验、按类别查询与软删除等方法。</para>
/// <para>所有方法通过 category 参数路由到 {Category}WholeConfig 物理表。</para>
/// </summary>
public interface IWholeConfigRepository : IRepository<WholeConfig>
{
    /// <summary>按主键 Id + 产品类别查询单条整机配置</summary>
    /// <param name="id">主键 Id</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW）</param>
    /// <returns>匹配的配置实体；不存在时返回 null</returns>
    Task<WholeConfig?> GetByIdAsync(long id, string category);

    /// <summary>按计划类型+产品编码+客户编码查询单条配置</summary>
    /// <param name="planType">检测计划类型</param>
    /// <param name="productCode">产品编码</param>
    /// <param name="customerCode">客户编码</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW）</param>
    /// <returns>匹配的配置实体；不存在时返回 null</returns>
    Task<WholeConfig?> GetByProductAndCustomerAsync(int planType, string productCode, string customerCode, string category);

    /// <summary>按多条件分页查询配置</summary>
    /// <param name="planType">检测计划类型筛选（为 null 时不过滤）</param>
    /// <param name="productCodeKeyword">产品编码模糊关键字</param>
    /// <param name="customerCodeKeyword">客户编码模糊关键字</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW）</param>
    /// <returns>分页结果</returns>
    Task<PagedResult<WholeConfig>> GetPagedAsync(int? planType, string productCodeKeyword, string customerCodeKeyword, int pageIndex, int pageSize, string category);

    /// <summary>判断计划类型+产品编码+客户编码组合是否已存在（用于唯一性校验）</summary>
    /// <param name="planType">检测计划类型</param>
    /// <param name="productCode">产品编码</param>
    /// <param name="customerCode">客户编码</param>
    /// <param name="excludeId">需排除的记录主键（编辑场景传入当前记录 Id，新增场景留空）</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW）</param>
    /// <returns>已存在返回 true，否则 false</returns>
    Task<bool> ProductCustomerExistsAsync(int planType, string productCode, string customerCode, long? excludeId, string category);

    /// <summary>按主键 Id + 产品类别软删除整机配置（IsDeleted = true）</summary>
    /// <param name="id">待删除实体的 Id</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW）</param>
    /// <returns>异步任务</returns>
    Task DeleteAsync(long id, string category);

    /// <summary>
    /// 按物料、插件、类型及产品和客户编码读取独立方案，不初始化数据库结构
    /// </summary>
    /// <param name="planType">方案类型：0=整机设置，1=整机检验</param>
    /// <param name="productCode">产品编码，按完整值精确匹配</param>
    /// <param name="customerCode">客户编码，按完整值精确匹配</param>
    /// <param name="category">产品类别，用于定位对应的整机方案表；支持 GasDG/GasOG/WaterDW/WaterOW</param>
    /// <param name="productKey">插件唯一标识（如 GasDG.AlarmHost），用于隔离不同插件的方案</param>
    /// <param name="materialNumber">软件物料号，须有效绑定当前插件；与插件标识、类型、产品及客户编码共同定位方案</param>
    /// <returns>匹配的未删除整机方案；物料绑定有效但未找到方案时返回 null</returns>
    Task<WholeConfig?> GetScopedAsync(int planType, string productCode, string customerCode, string category,
        string productKey, string materialNumber);

    /// <summary>
    /// 分页查询有效绑定的插件、物料及方案类型下的有效方案，可按编码筛选
    /// </summary>
    /// <param name="planType">方案类型：0=整机设置，1=整机检验</param>
    /// <param name="productCodeKeyword">产品编码模糊关键字，可为空</param>
    /// <param name="customerCodeKeyword">客户编码模糊关键字，可为空</param>
    /// <param name="pageIndex">页码，从1开始</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="category">产品类别，支持 GasDG/GasOG/WaterDW/WaterOW</param>
    /// <param name="productKey">插件标识，须与产品类别匹配</param>
    /// <param name="materialNumber">软件物料号，须有效绑定当前插件</param>
    /// <returns>匹配的有效方案分页结果，按更新时间及主键倒序排列</returns>
    Task<PagedResult<WholeConfig>> GetScopedPagedAsync(int planType, string productCodeKeyword, string customerCodeKeyword,
        int pageIndex, int pageSize, string category, string productKey, string materialNumber);

    /// <summary>
    /// 核对有效绑定，在同一事务中按主键及完整范围软删除方案并写删除审计
    /// </summary>
    /// <param name="id">选中方案的主键</param>
    /// <param name="planType">方案类型（0=设置，1=检验）</param>
    /// <param name="productCode">选中方案的产品编码</param>
    /// <param name="customerCode">选中方案的客户编码</param>
    /// <param name="category">产品类别</param>
    /// <param name="productKey">插件标识</param>
    /// <param name="materialNumber">软件物料号</param>
    /// <param name="operatorWorkNumber">当前操作工号</param>
    /// <returns>是否删除成功，不存在或已删除时返回false</returns>
    Task<bool> DeleteScopedAsync(long id, int planType, string productCode, string customerCode,
        string category, string productKey, string materialNumber, string operatorWorkNumber);

    /// <summary>
    /// 在一个事务中核对有效绑定，保存方案与审计；未变化时不更新
    /// </summary>
    /// <param name="planType">方案类型：0=整机设置，1=整机检验</param>
    /// <param name="productCode">产品编码，按完整值精确匹配</param>
    /// <param name="customerCode">客户编码，按完整值精确匹配</param>
    /// <param name="category">产品类别，用于定位对应的整机方案表；支持 GasDG/GasOG/WaterDW/WaterOW</param>
    /// <param name="productKey">插件唯一标识（如 GasDG.AlarmHost），用于隔离不同插件的方案</param>
    /// <param name="materialNumber">软件物料号，须有效绑定当前插件；与插件标识、类型、产品及客户编码共同定位方案</param>
    /// <param name="stationCount">方案的工位数量，至少为 1</param>
    /// <param name="parametersJson">方案项目参数的 JSON 数组，写入 ParametersJson，包含勾选状态、MES 项目码及具体参数值</param>
    /// <param name="operatorWorkNumber">当前登录操作员工号，用于方案的创建人、修改人和审计记录的操作人字段</param>
    /// <returns>新增或维护后的整机方案；内容未变化时返回原方案，不更新方案或新增审计记录</returns>
    Task<WholeConfig> SaveScopedAsync(int planType, string productCode, string customerCode, string category,
        string productKey, string materialNumber, int stationCount, string parametersJson,
        string operatorWorkNumber);

}
