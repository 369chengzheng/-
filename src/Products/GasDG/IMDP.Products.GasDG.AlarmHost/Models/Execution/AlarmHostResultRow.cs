using System.Collections.ObjectModel;
using IMDP.Application.DTOs;

namespace IMDP.Products.GasDG.AlarmHost.Models;

/// <summary>
/// 一个已勾选项目的结果行，方案信息由整行共用，结果按工位独立保存
/// </summary>
public sealed class AlarmHostResultRow
{
    public AlarmHostResultRow(int index, TestParameterDto item, int stationCount)
    {
        Index = index;
        Code = item.Code;
        Name = item.Name;
        MesCode = item.MesCode;
        Order = item.Order;
        Parameters = new ReadOnlyDictionary<string, string>(item.Parameters.ToDictionary(
            parameter => parameter.Code, parameter => parameter.Value, StringComparer.Ordinal));
        Cells = Enumerable.Range(1, stationCount).Select(number => new AlarmHostResultCell(number)).ToArray();
        if (!CanExecute)
            foreach (var cell in Cells) cell.Clear("执行未接入");
    }

    /// <summary>
    /// 连续显示序号，从一开始，与功能编码分开
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// 稳定功能编码，后续指令及结果回填以此定位，不依赖行下标
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// 方案保存的项目名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 当前项目关联的 MES 项目码，本阶段不上传
    /// </summary>
    public string MesCode { get; }

    /// <summary>
    /// 方案原有项目顺序，保留供后续执行使用，不被显示序号覆盖
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// 按子参数编码取值的只读快照，保留原值及空格，不使用预览转换后的中文文本
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    /// <summary>
    /// 当前功能及参数形态是否已有设备执行实现，新增展示项不冒充可执行项目
    /// </summary>
    public bool CanExecute => AlarmHostFunctionCatalog.CanExecute(Code);

    /// <summary>
    /// 按连续工位号排列的独立结果格，未接入项目显示中性提示
    /// </summary>
    public IReadOnlyList<AlarmHostResultCell> Cells { get; }
}
