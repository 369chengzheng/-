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
