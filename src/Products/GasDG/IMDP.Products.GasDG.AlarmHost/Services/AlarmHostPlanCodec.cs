using System.Globalization;
using IMDP.Application.DTOs;
using IMDP.Domain.Products;
using IMDP.Products.GasDG.AlarmHost.Models;

namespace IMDP.Products.GasDG.AlarmHost.Services;

/// <summary>
/// 将平台返回的公共方案 DTO 校验并回填为报警编辑草稿，不访问数据库或设备
/// </summary>
public static class AlarmHostPlanCodec
{
    /// <summary>
    /// 核对方案业务范围，将已启用项目的 MES 码和参数一次性回填到新草稿
    /// 缺少必要参数时提示对应项目及参数名，不在异常中包含参数值
    /// </summary>
    /// <param name="plan">
    /// 平台查询返回的方案 DTO，回填过程不修改原对象
    /// </param>
    /// <param name="expectedIdentity">
    /// 当前物料、方案类型、产品编码和客户编码的预期范围
    /// </param>
    /// <remarks>
    /// 同时核对公共 DTO 的插件归属，防止其他插件的同编码方案被回填
    /// </remarks>
    public static AlarmHostConfigDraft ToDraft(TestPlanDto plan, AlarmHostPlanIdentity expectedIdentity)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(expectedIdentity);
        if (plan.ProductKey != expectedIdentity.ProductKey ||
            plan.MaterialNumber != expectedIdentity.MaterialNumber || plan.Category != ProductCategory.GasDG ||
            plan.PlanType != expectedIdentity.PlanType || plan.ProductCode != expectedIdentity.ProductCode ||
            plan.CustomerCode != expectedIdentity.CustomerCode)
            throw new InvalidOperationException("方案与当前物料、类型或产品/客户编码不匹配，请重新查询。");
        if (plan.Parameters == null)
            throw new InvalidOperationException("方案缺少功能项目列表。");

        var draft = new AlarmHostConfigDraft
        {
            ProductCode = plan.ProductCode, CustomerCode = plan.CustomerCode,
            StationCount = plan.StationCount.ToString(CultureInfo.InvariantCulture)
        };
        var selectedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in plan.Parameters)
        {
            if (item == null) throw new InvalidOperationException("方案包含空的功能项目。");
            if (!item.IsEnabled) continue;
            var definition = AlarmHostFunctionCatalog.All.FirstOrDefault(function => function.Key == item.Code);
            if (definition == null) throw new InvalidOperationException("方案包含未支持的功能项目。");
            if (!selectedKeys.Add(item.Code))
                throw new InvalidOperationException($"{definition.DisplayName}项目重复。");
            if (item.Parameters == null)
                throw new InvalidOperationException($"{definition.DisplayName}缺少子参数列表。");

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var parameter in item.Parameters)
            {
                if (parameter == null || string.IsNullOrEmpty(parameter.Code) || parameter.Value == null)
                    throw new InvalidOperationException($"{definition.DisplayName}的子参数缺少名称或值。");
                if (!values.TryAdd(parameter.Code, parameter.Value))
                    throw new InvalidOperationException($"{definition.DisplayName}存在重复子参数。");
            }
            var target = draft.Items[definition.Key];
            target.IsEnabled = true;
            target.MesCode = item.MesCode ?? "";
            foreach (var pair in values)
            {
                if (!target.Values.TryGetValue(pair.Key, out var parameter))
                    throw new InvalidOperationException($"{definition.DisplayName}包含当前页面尚未支持的子参数，请核对方案。");
                parameter.Value = pair.Value;
            }
            var required = AlarmHostFunctionCatalog.ActiveParameters(target.ToDto()).Select(parameter => parameter.Code).ToArray();
            foreach (var key in required)
                if (!values.ContainsKey(key) && item.Code is not (AlarmHostFunctionCatalog.Csq or AlarmHostFunctionCatalog.Version))
                    throw new InvalidOperationException($"{definition.DisplayName}缺少 {key} 参数。");
            if (values.Keys.Except(required, StringComparer.Ordinal).Any())
                throw new InvalidOperationException($"{definition.DisplayName}包含当前分支尚未支持的子参数，请核对方案。");
        }
        if (selectedKeys.Count == 0)
            throw new InvalidOperationException("方案至少应启用一个适用项目。");
        var errors = draft.GetInputErrors();
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        return draft;
    }
}
