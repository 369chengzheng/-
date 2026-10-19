// =============================================================================
// 文件名：LaserDetectorStationState.cs
// 功能描述：激光检测仪校验界面单工位运行时状态模型。
//           承载工位状态色、状态文本、表具条码、自动测试结果展示项、
//           测试耗时与人工测试项，供 LaserDetectorView 工位卡片绑定。
// =============================================================================
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IMDP.Products.GasDG.LaserDetector.Models;

/// <summary>
/// 单工位运行时状态
/// <para>每个检测工位一份实例，承载界面绑定的全部动态状态：
/// 状态头背景色与文本、表具条码、自动测试结果展示项、测试耗时、
/// 人工测试项（LED/蜂鸣器/按键）。</para>
/// </summary>
public partial class LaserDetectorStationState : ObservableObject
{
    /// <summary>人工测试项的可选状态列表（正常/异常）</summary>
    public static readonly string[] ManualOptions = { "正常", "异常" };

    /// <summary>工位号（1～3）</summary>
    public int Station { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="station">工位号</param>
    public LaserDetectorStationState(int station)
    {
        Station = station;
    }

    /// <summary>表具条码（扫码枪输入）</summary>
    [ObservableProperty]
    private string _equipmentCode = string.Empty;

    /// <summary>工位状态头背景色（Gray/Orange/Green/Red）</summary>
    [ObservableProperty]
    private string _statusColor = "Gray";

    /// <summary>工位状态头显示文本（等待扫码/自动测试中/人工测试中/合格/不合格）</summary>
    [ObservableProperty]
    private string _statusText = "等待扫码";

    /// <summary>测试累计耗时展示（mm:ss，测试进行中每秒刷新）</summary>
    [ObservableProperty]
    private string _testDuration = "00:00";

    /// <summary>自动检测是否已通过（未完成时人工项不参与最终判定）</summary>
    [ObservableProperty]
    private bool _isAutoPassed;

    /// <summary>自动检测是否仍在进行中（进行中时禁止重复触发）</summary>
    [ObservableProperty]
    private bool _isAutoTesting;

    /// <summary>LED 指示灯人工测试结果</summary>
    [ObservableProperty]
    private string _ledResult = ManualOptions[0];

    /// <summary>蜂鸣器人工测试结果</summary>
    [ObservableProperty]
    private string _buzzerResult = ManualOptions[0];

    /// <summary>按键人工测试结果</summary>
    [ObservableProperty]
    private string _buttonResult = ManualOptions[0];

    /// <summary>自动测试结果展示项（只读，检测完成后填充）</summary>
    [ObservableProperty]
    private ObservableCollection<LaserDetectorResultDisplayItem> _propertyItems = new();

    /// <summary>
    /// 重置工位到新一轮检测的初始状态（保留工位号）
    /// </summary>
    public void Reset()
    {
        EquipmentCode = string.Empty;
        StatusColor = "Gray";
        StatusText = "等待扫码";
        TestDuration = "00:00";
        IsAutoPassed = false;
        IsAutoTesting = false;
        LedResult = ManualOptions[0];
        BuzzerResult = ManualOptions[0];
        ButtonResult = ManualOptions[0];
        PropertyItems = new ObservableCollection<LaserDetectorResultDisplayItem>();
    }
}
