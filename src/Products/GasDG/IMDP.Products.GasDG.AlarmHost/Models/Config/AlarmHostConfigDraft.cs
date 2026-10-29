using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IMDP.Products.GasDG.AlarmHost.Models;

/// <summary>
/// 方案编辑草稿；每个功能持有独立公共 DTO，数字保留原始输入
/// </summary>
public sealed partial class AlarmHostConfigDraft : ObservableObject
{
    /// <summary>
    /// 当前方案的产品编码
    /// </summary>
    [ObservableProperty]
    private string _productCode = "";

    /// <summary>
    /// 当前方案的客户编码
    /// </summary>
    [ObservableProperty]
    private string _customerCode = "";

    /// <summary>
    /// 工位数量原始输入，检查时转换为正整数
    /// </summary>
    [ObservableProperty]
    private string _stationCount = "4";

    public AlarmHostConfigDraft()
    {
        Items = new System.Collections.ObjectModel.ReadOnlyDictionary<string, AlarmHostConfigItem>(
            AlarmHostFunctionCatalog.All.ToDictionary(definition => definition.Key,
                definition => new AlarmHostConfigItem(definition), StringComparer.Ordinal));
        foreach (var item in Items.Values)
            item.PropertyChanged += (_, e) =>
            {
                OnPropertyChanged(nameof(Items));
                if (item.Code != AlarmHostFunctionCatalog.MainPowerReport || e.PropertyName != nameof(AlarmHostConfigItem.Values)) return;
                OnPropertyChanged(nameof(UsesSpecifiedSchedule));
                OnPropertyChanged(nameof(UsesOrdinarySchedule));
                OnPropertyChanged(nameof(ReportHint));
            };
    }

    /// <summary>
    /// 全部可编辑功能，按编码访问；新目录项默认未勾选，加载旧方案也可显示
    /// </summary>
    public IReadOnlyDictionary<string, AlarmHostConfigItem> Items { get; }

    /// <summary>
    /// 按功能目录顺序展示仅MES项目，直接复用当前草稿中的可编辑项
    /// </summary>
    public IEnumerable<AlarmHostConfigItem> NoParameterItems => AlarmHostFunctionCatalog.All
        .Where(definition => !definition.HasVisibleParameters).Select(definition => Items[definition.Key]);

    /// <summary>
    /// 需要展示参数控件的项目按显示顺序排列，宽组集中在末尾
    /// </summary>
    public IEnumerable<AlarmHostConfigItem> ParameterItems => AlarmHostFunctionCatalog.DisplayOrder
        .Where(definition => definition.HasVisibleParameters)
        .Select(definition => Items[definition.Key]);

    /// <summary>
    /// 回填已完整解码的草稿，保留编辑项及控件绑定；未启用项目也恢复源草稿默认值，避免方案间残留
    /// </summary>
    public void ApplyFrom(AlarmHostConfigDraft source)
    {
        ProductCode = source.ProductCode;
        CustomerCode = source.CustomerCode;
        StationCount = source.StationCount;
        foreach (var item in Items.Values)
        {
            var incoming = source.Items[item.Code];
            item.IsEnabled = incoming.IsEnabled;
            item.MesCode = incoming.MesCode;
            foreach (var parameter in item.Values)
                parameter.Value.Value = incoming.Values[parameter.Key].Value;
        }
    }

    /// <summary>
    /// 主电上报当前选中的类型，空值表示未选择
    /// </summary>
    private byte? CurrentReportType => Items[AlarmHostFunctionCatalog.MainPowerReport].Values["Type"].ByteValue;

    /// <summary>
    /// 类型 7/8 显示五项 HHmm（指定五时）或 ddHH（指定五日）输入
    /// </summary>
    public bool UsesSpecifiedSchedule => CurrentReportType is 7 or 8;

    /// <summary>
    /// 其他类型显示周期、日、时、分，切换时保留另一分支已填值
    /// </summary>
    public bool UsesOrdinarySchedule => !UsesSpecifiedSchedule;

    /// <summary>
    /// 当前上报类型的输入格式提示
    /// </summary>
    public string ReportHint => CurrentReportType switch
    {
        7 => "指定五时：每项填写 HHmm，例如 0830。",
        8 => "指定五日：每项填写 ddHH，例如 1508。",
        _ => "普通上报：填写周期、日、时、分；切换类型保留已填值。"
    };

    /// <summary>
    /// 检查页面输入形态；设备/固件规则留到协议接入阶段核对
    /// </summary>
    public IReadOnlyList<string> GetInputErrors()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(ProductCode)) errors.Add("请填写产品编码。");
        if (string.IsNullOrWhiteSpace(CustomerCode)) errors.Add("请填写客户编码。");
        if (!int.TryParse(StationCount?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var stations) || stations < 1)
            errors.Add("工位数量应为正整数。");
        foreach (var item in Items.Values.Where(item => item.IsEnabled))
            errors.AddRange(AlarmHostFunctionCatalog.GetInputErrors(item.ToDto()));
        return errors;
    }
}
