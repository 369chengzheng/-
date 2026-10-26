// =============================================================================
// 文件名：LaserDetectorStationState.cs
// 功能描述：激光检测仪校验界面单工位运行时状态模型（严格 MVVM）。
//           以 Phase 枚举表达检测阶段（界面表现由 XAML 触发器映射），
//           承载表具条码、自动测试结果展示项、测试耗时与人工测试项。
// =============================================================================
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IMDP.Products.GasDG.LaserDetector.Models;

/// <summary>
/// 工位检测阶段枚举
/// <para>ViewModel 只负责状态迁移（业务语义）；界面背景色与状态文案
/// 由 XAML DataTrigger 按枚举映射（界面表现）。</para>
/// </summary>
public enum LaserDetectorStationPhase
{
    /// <summary>等待扫码（灰色）</summary>
    Idle,

    /// <summary>自动测试中（橙色）</summary>
    AutoTesting,

    /// <summary>人工测试中（橙色）</summary>
    ManualTesting,

    /// <summary>自动测试通过，等待人工测试（橙色）</summary>
    AwaitingManual,

    /// <summary>检测合格（绿色）</summary>
    Passed,

    /// <summary>检测不合格（红色）</summary>
    Failed,

    /// <summary>串口通信失败（红色）</summary>
    CommFailed,

    /// <summary>MES 上传失败（红色；测试本身完成但上传 MES 被拒/异常，本轮按不合格处理）</summary>
    MesFailed,

    /// <summary>
    /// 检测超时（红色；查询指令 0xAC 轮询耗尽总超时未收到结果帧，本轮按不合格处理）
    /// <para>界面报错优先级最高：超时 &gt; 检测项不通过 &gt; MES 上传失败，
    /// 超时状态下后续 MES 上传成败均不覆盖本状态。</para>
    /// </summary>
    Timeout
}

/// <summary>
/// 单工位运行时状态
/// <para>每个检测工位一份实例，承载界面绑定的全部动态状态：
/// 检测阶段（Phase）、表具条码、自动测试结果展示项、测试耗时、
/// 人工测试项（LED/蜂鸣器/按键）。</para>
/// </summary>
public partial class LaserDetectorStationState : ObservableObject
{
    /// <summary>人工测试项的可选状态列表（正常/异常）</summary>
    public static readonly string[] ManualOptions = { "正常", "异常" };

    /// <summary>工位号（正整数，来源数据库方案配置）</summary>
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

    /// <summary>当前检测阶段（界面据此映射背景色与文案）</summary>
    [ObservableProperty]
    private LaserDetectorStationPhase _phase = LaserDetectorStationPhase.Idle;

    /// <summary>测试累计耗时展示（m:ss，测试进行中每秒刷新）</summary>
    [ObservableProperty]
    private string _testDuration = "00:00";

    /// <summary>测试累计耗时（TimeSpan，供状态行按 m:ss 格式化展示）</summary>
    [ObservableProperty]
    private TimeSpan _testDurationTime = TimeSpan.Zero;

    /// <summary>自动检测是否已通过（未完成时人工项不参与最终判定）</summary>
    [ObservableProperty]
    private bool _isAutoPassed;

    /// <summary>自动检测是否仍在进行中（进行中时禁止重复触发）</summary>
    [ObservableProperty]
    private bool _isAutoTesting;

    /// <summary>
    /// 本轮结果是否已上报（MES 上传 + 本地记录保存）
    /// <para>扫码开始新一轮检测时重置；人工项判定与自动失败路径据此保证每轮只上报一次
    /// （对应 IMDS _inspectionSaved 防重标志）。</para>
    /// </summary>
    [ObservableProperty]
    private bool _isResultReported;

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
        Phase = LaserDetectorStationPhase.Idle;
        TestDuration = "00:00";
        TestDurationTime = TimeSpan.Zero;
        IsAutoPassed = false;
        IsAutoTesting = false;
        IsResultReported = false;
        LedResult = ManualOptions[0];
        BuzzerResult = ManualOptions[0];
        ButtonResult = ManualOptions[0];
        PropertyItems = new ObservableCollection<LaserDetectorResultDisplayItem>();
    }

    /// <summary>
    /// 仅复位人工测试项（LED/蜂鸣器/按键）为默认"正常"
    /// <para>重新扫码触发新一轮检测时调用，人工项默认全部正常。</para>
    /// </summary>
    public void ResetManualResults()
    {
        LedResult = ManualOptions[0];
        BuzzerResult = ManualOptions[0];
        ButtonResult = ManualOptions[0];
    }
}
