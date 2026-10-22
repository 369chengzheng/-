using System.Text.Json;
using IMDP.Application.DTOs;

namespace IMDP.Products.GasDG.AlarmHost.Models;

/// <summary>
/// 报警方案的业务范围；物料绑定有效性由后续存储入口核对
/// </summary>
public sealed class AlarmHostPlanIdentity
{
    public AlarmHostPlanIdentity(string materialNumber, int planType, string productCode, string customerCode)
    {
        if (!AlarmHostPlanTypes.IsValid(planType)) throw new ArgumentOutOfRangeException(nameof(planType));
        MaterialNumber = Required(materialNumber, nameof(materialNumber));
        PlanType = planType;
        ProductCode = Required(productCode, nameof(productCode));
        CustomerCode = Required(customerCode, nameof(customerCode));
    }

    /// <summary>
    /// 当前方案所属的软件物料号
    /// </summary>
    public string MaterialNumber { get; }
    /// <summary>
    /// 当前方案所属的报警主机插件标识，固定为 GasDG.AlarmHost
    /// </summary>
    public string ProductKey => AlarmHostFunctionCatalog.ProductKey;
    /// <summary>
    /// 方案类型，0=设置、1=检验
    /// </summary>
    public int PlanType { get; }
    /// <summary>
    /// 当前方案的产品编码
    /// </summary>
    public string ProductCode { get; }
    /// <summary>
    /// 当前方案的客户编码
    /// </summary>
    public string CustomerCode { get; }

    private static string Required(string? value, string name) => !string.IsNullOrWhiteSpace(value)
        ? value.Trim() : throw new ArgumentException("必须提供非空值", name);
}

/// <summary>
/// 公共 TestPlanDto 的独立副本，补充报警插件范围及 JSON 格式版本
/// 方案、项目及子参数均复用公共 DTO；这里不重复定义参数容器
/// JSON 可能包含网络密码，不能完整写入日志；文档不代表已经保存或设备协议校验通过
/// </summary>
public sealed class AlarmHostPlanDocument
{
    /// <summary>
    /// 采集时保存的公共方案 DTO 深副本，作为当前文档的内部数据
    /// </summary>
    private readonly TestPlanDto _plan;

    internal AlarmHostPlanDocument(AlarmHostPlanIdentity identity, TestPlanDto plan)
    {
        Identity = identity;
        // 公共 DTO 可编辑，采集时及对外返回时都复制，避免外部修改污染已采集内容
        _plan = Copy(plan);
        ContentJson = JsonSerializer.Serialize(new { SchemaVersion, identity.ProductKey, Plan = _plan });
    }

    /// <summary>
    /// 插件文档的 JSON 格式版本，不是数据库并发版本
    /// </summary>
    public int SchemaVersion => 1;
    /// <summary>
    /// 当前文档的物料、插件、方案类型、产品编码和客户编码范围
    /// </summary>
    public AlarmHostPlanIdentity Identity { get; }
    /// <summary>
    /// 方案配置的工位数量
    /// </summary>
    public int StationCount => _plan.StationCount;
    /// <summary>
    /// 方案中已勾选并采集的功能项目数量
    /// </summary>
    public int FunctionCount => _plan.Parameters.Count;
    /// <summary>
    /// 包含格式版本、插件标识及公共方案 DTO 的 JSON 文档，可能包含网络密码
    /// </summary>
    public string ContentJson { get; }

    /// <summary>
    /// 返回公共方案 DTO 的深副本，可交给后续平台服务适配，不暴露内部可变引用
    /// </summary>
    public TestPlanDto ToPlanDto() => Copy(_plan);

    private static TestPlanDto Copy(TestPlanDto plan) =>
        JsonSerializer.Deserialize<TestPlanDto>(JsonSerializer.Serialize(plan))!;
}
