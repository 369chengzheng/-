using System.Globalization;
using IMDP.Application.DTOs;
using IMDP.Domain.Products;
using IMDP.Products.GasDG.AlarmHost.Models;

namespace IMDP.Products.GasDG.AlarmHost.Services;

/// <summary>
/// 从公共功能 DTO 采集当前勾选项，不为每个功能另写字段映射
/// </summary>
public static class AlarmHostPlanFactory
{
    /// <summary>
    /// 检查草稿并生成独立方案副本，仅包含当前类型适用且已勾选的功能及有效分支参数
    /// </summary>
    public static AlarmHostPlanDocument FromDraft(string materialNumber, int planType, AlarmHostConfigDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var errors = draft.GetInputErrors();
        if (errors.Count > 0) throw new ArgumentException(string.Join("\n", errors));
        var identity = new AlarmHostPlanIdentity(materialNumber, planType, draft.ProductCode, draft.CustomerCode);
        var items = new List<TestParameterDto>();
        foreach (var definition in AlarmHostFunctionCatalog.All)
        {
            var source = draft.Items[definition.Key];
            if (!source.IsEnabled) continue;
            if (!definition.Supports(planType)) throw new ArgumentException("当前方案类型不允许所选项目");
            var item = source.ToDto();
            item.MesCode = item.MesCode.Trim();
            item.Order = items.Count + 1;
            item.Parameters = AlarmHostFunctionCatalog.ActiveParameters(item).ToList();
            for (var i = 0; i < item.Parameters.Count; i++) item.Parameters[i].Order = i + 1;
            items.Add(item);
        }
        if (items.Count == 0) throw new ArgumentException("方案至少应启用一个适用项目");
        return new AlarmHostPlanDocument(identity, new TestPlanDto
        {
            ProductKey = identity.ProductKey,
            MaterialNumber = identity.MaterialNumber, ProductCode = identity.ProductCode,
            CustomerCode = identity.CustomerCode, Category = ProductCategory.GasDG, PlanType = identity.PlanType,
            StationCount = int.Parse(draft.StationCount.Trim(), CultureInfo.InvariantCulture), Parameters = items
        });
    }
}
