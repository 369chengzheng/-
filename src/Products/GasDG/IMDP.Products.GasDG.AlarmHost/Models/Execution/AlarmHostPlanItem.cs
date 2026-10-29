using IMDP.Application.DTOs;

namespace IMDP.Products.GasDG.AlarmHost.Models;

/// <summary>
/// 执行页的只读项目展示；显示文本与保存的原始参数值分开
/// </summary>
public sealed class AlarmHostPlanItem
{
    /// <summary>
    /// 配置目录中的功能编码，用于关联当前预览项目
    /// </summary>
    public string Code { get; private init; } = "";

    /// <summary>
    /// 功能中文名称
    /// </summary>
    public string Name { get; private init; } = "";

    /// <summary>
    /// 当前方案维护的 MES 项目码
    /// </summary>
    public string MesCode { get; private init; } = "";

    /// <summary>
    /// 公共只读模板使用的参数、控件类型及选项
    /// </summary>
    public IReadOnlyList<AlarmHostPlanParameter> Parameters { get; private init; } = [];

    /// <summary>
    /// 字段总宽度超出普通组空间时使用宽组，短范围字段仍可同行
    /// </summary>
    public bool IsComplex => AlarmHostFunctionCatalog.Get(Code).IsComplex;

    /// <summary>
    /// 将已通过报警解码规则的启用项目转换为只读控件数据，不修改公共 DTO
    /// </summary>
    public static AlarmHostPlanItem FromParameter(TestParameterDto item)
    {
        var definition = AlarmHostFunctionCatalog.Get(item.Code);
        var parameters = definition.ShowParameters
            ? item.Parameters.OrderBy(parameter => parameter.Order).Select(parameter =>
                AlarmHostPlanParameter.FromParameter(parameter, definition.GetParameter(parameter.Code))).ToArray()
            : Array.Empty<AlarmHostPlanParameter>();
        return new AlarmHostPlanItem
        {
            Code = item.Code,
            Name = item.Name,
            MesCode = item.MesCode,
            Parameters = parameters
        };
    }
}
