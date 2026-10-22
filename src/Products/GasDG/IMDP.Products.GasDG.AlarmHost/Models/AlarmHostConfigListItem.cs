using IMDP.Application.DTOs;

namespace IMDP.Products.GasDG.AlarmHost.Models;

/// <summary>
/// 实际查询或保存方案的只读列表显示形态
/// </summary>
public sealed class AlarmHostConfigListItem
{
    /// <summary>
    /// 对应的实际方案，用于选中列表记录后回填编辑区域
    /// </summary>
    internal TestPlanDto? Plan { get; private init; }
    /// <summary>
    /// 列表中方案的产品编码
    /// </summary>
    public string ProductCode { get; init; } = "";
    /// <summary>
    /// 列表中方案的客户编码
    /// </summary>
    public string CustomerCode { get; init; } = "";
    /// <summary>
    /// 列表中方案配置的工位数量
    /// </summary>
    public int StationCount { get; init; }
    /// <summary>
    /// 方案创建人的显示值
    /// </summary>
    public string CreatedBy { get; init; } = "";
    /// <summary>
    /// 方案创建时间，空值表示未提供
    /// </summary>
    public DateTime? CreatedTime { get; init; }
    /// <summary>
    /// 方案最近维护人的显示值
    /// </summary>
    public string UpdatedBy { get; init; } = "";
    /// <summary>
    /// 方案最近维护时间，空值表示未提供
    /// </summary>
    public DateTime? UpdatedTime { get; init; } 
    /// <summary>
    /// 按功能编码保存实际方案的显示文本
    /// </summary>
    public IReadOnlyDictionary<string, string> Items { get; init; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
            AlarmHostFunctionCatalog.All.ToDictionary(item => item.Key, _ => "", StringComparer.Ordinal));

    /// <summary>
    /// 将实际查询或保存结果投影到列表，参数按原值显示
    /// </summary>
    public static AlarmHostConfigListItem FromPlan(TestPlanDto plan) => new()
    {
        Plan = plan,
        ProductCode = plan.ProductCode, CustomerCode = plan.CustomerCode, StationCount = plan.StationCount,
        CreatedBy = plan.CreatedBy, CreatedTime = plan.CreatedTime, UpdatedBy = plan.UpdatedBy, UpdatedTime = plan.UpdatedTime,
        Items = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
            AlarmHostFunctionCatalog.All.ToDictionary(definition => definition.Key, definition =>
            {
                var item = plan.Parameters.FirstOrDefault(parameter => parameter.Code == definition.Key && parameter.IsEnabled);
                if (item == null) return "未启用";
                return item.Parameters.Count == 0 ? "启用" : string.Join("；", item.Parameters.Select(parameter => parameter.Value));
            }, StringComparer.Ordinal))
    };
}
