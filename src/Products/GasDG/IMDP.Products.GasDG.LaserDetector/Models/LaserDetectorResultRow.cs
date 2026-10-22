// =============================================================================
// 文件名：LaserDetectorResultRow.cs
// 功能描述：激光检测仪校验界面表格行视图模型（严格 MVVM）。
//           以"行 = 检测项目、列 = 工位"的矩阵组织表格内容：
//           固定行（红外串口/检测状态/表具条码/LED/蜂鸣器/按键）+
//           配置项自动检测结果行（序号自 7 起）；每行承载序号、项目名称与
//           各工位结果单元格；每个单元格含实测值与红绿判定，检测完成后由
//           ViewModel 填充。
// =============================================================================
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IMDP.Products.GasDG.LaserDetector.Models;

/// <summary>
/// 自动检测结果表格行
/// <para>一行对应一个表格行：序号 + 项目名称为表格左侧固定列，
/// 单元格集合与工位一一对应（下标 = 工位号 - 1），检测完成后填充实测值；
/// 固定行（红外串口/检测状态/表具条码/人工项）与配置项行共用此结构。</para>
/// </summary>
public partial class LaserDetectorResultRow : ObservableObject
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="index">序号（1 起）</param>
    /// <param name="name">检测项目名称（如 12V电压）</param>
    /// <param name="stationCount">工位数量（决定单元格列数）</param>
    /// <param name="kind">行类型（决定单元格模板：文本/下拉/条码输入）</param>
    public LaserDetectorResultRow(int index, string name, int stationCount, LaserDetectorResultRowKind kind)
    {
        Index = index;
        Name = name;
        Kind = kind;
        Cells = new ObservableCollection<LaserDetectorResultCell>(
            Enumerable.Range(1, stationCount).Select(station => new LaserDetectorResultCell(station)));
    }

    /// <summary>序号（1 起）</summary>
    public int Index { get; }

    /// <summary>检测项目名称</summary>
    public string Name { get; }

    /// <summary>行类型（决定单元格展示模板：自动结果文本/人工下拉/条码输入）</summary>
    public LaserDetectorResultRowKind Kind { get; }

    /// <summary>各工位结果单元格（下标 = 工位号 - 1，与工位列对齐）</summary>
    public ObservableCollection<LaserDetectorResultCell> Cells { get; }
}

/// <summary>
/// 表格行类型枚举
/// <para>界面按行类型选择单元格模板：文本结果（默认）、人工下拉、条码输入框。</para>
/// </summary>
public enum LaserDetectorResultRowKind
{
    /// <summary>自动检测结果行：红绿文本单元格</summary>
    Auto,

    /// <summary>人工测试项行：单元格内嵌正常/异常下拉框（选正常绿/异常红）</summary>
    Manual,

    /// <summary>表具条码行：单元格内嵌扫码输入框（回车触发检测并自动跳下一工位）</summary>
    Barcode
}

/// <summary>
/// 自动检测结果单元格（单工位 × 单检测项）
/// <para>待检测时 Value 为空文本仅占位；检测完成后写入实测值并按判定
/// 呈现红/绿底色（IsPassed 仅在 HasResult 为 true 时参与界面判定）。</para>
/// </summary>
public partial class LaserDetectorResultCell : ObservableObject
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="station">所属工位号</param>
    public LaserDetectorResultCell(int station)
    {
        Station = station;
    }

    /// <summary>所属工位号（供调试与定位使用）</summary>
    public int Station { get; }

    /// <summary>实测值展示文本（待检测时为空；人工行未启用）</summary>
    [ObservableProperty]
    private string _value = string.Empty;

    /// <summary>该项是否判定通过（true=绿色，false=红色）</summary>
    [ObservableProperty]
    private bool _isPassed;

    /// <summary>是否已有检测结果（控制占位/结果样式切换）</summary>
    [ObservableProperty]
    private bool _hasResult;

    /// <summary>人工判定结果（仅人工行使用：正常/异常；默认正常）</summary>
    [ObservableProperty]
    private string _manualResult = LaserDetectorStationState.ManualOptions[0];

    /// <summary>
    /// 单元格底色模式（空=中性灰白；green=绿；red=红；orange=橙）
    /// <para>由 ViewModel 按行语义计算写入：自动行按红绿判定、人工行按
    /// 正常/异常、状态行按检测阶段（检测中橙色）、红外串口行按串口开关；
    /// 界面触发器按此值切换 Background。</para>
    /// </summary>
    [ObservableProperty]
    private string _colorMode = string.Empty;

    /// <summary>是否人工测试行单元格（控制下拉框可见性）</summary>
    public bool IsManual { get; set; }

    /// <summary>表具条码（仅条码行使用；与工位状态 EquipmentCode 双向同步）</summary>
    [ObservableProperty]
    private string _equipmentCode = string.Empty;
}
