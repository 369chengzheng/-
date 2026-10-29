using CommunityToolkit.Mvvm.ComponentModel;

namespace IMDP.Products.GasDG.AlarmHost.Models;

/// <summary>
/// 一个项目在一个工位的实际结果值，不存放方案参数或预设合格状态
/// </summary>
public sealed class AlarmHostResultCell(int stationNumber) : ObservableObject
{
    /// <summary>
    /// 尚未执行时结果为空
    /// </summary>
    private string _value = "";

    /// <summary>
    /// 实际MES纯值，不从带标签的显示文字反向还原
    /// </summary>
    private string _mesValue = "";

    /// <summary>
    /// 本项执行判定，未执行或等待时为空
    /// </summary>
    private bool? _isPass;

    /// <summary>
    /// 本项失败原因或成功说明
    /// </summary>
    private string _message = "";

    /// <summary>
    /// 区分尚未执行与已读取但不参与合格判定的结果。
    /// </summary>
    public bool HasResult { get; private set; }

    /// <summary>
    /// 结果所属工位号，从一开始
    /// </summary>
    public int StationNumber { get; } = stationNumber;

    /// <summary>
    /// 实际执行结果文本，多值结果按英文逗号连接
    /// </summary>
    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    /// <summary>
    /// 后续上传MES使用的纯值，项目码取自所属结果行
    /// </summary>
    public string MesValue
    {
        get => _mesValue;
        private set => SetProperty(ref _mesValue, value);
    }

    /// <summary>
    /// 设备读取格式及目标核对的判定
    /// </summary>
    public bool? IsPass
    {
        get => _isPass;
        private set => SetProperty(ref _isPass, value);
    }

    /// <summary>
    /// 提供鼠标查看的实际值及判定说明
    /// </summary>
    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    /// <summary>
    /// 新一轮执行或扫码时统一清除旧值、MES值及判定
    /// </summary>
    public void Clear(string value = "")
    {
        HasResult = false;
        Value = value;
        MesValue = "";
        IsPass = null;
        Message = "";
    }

    /// <summary>
    /// 将同一次设备结果一并回填，不更新其他工位
    /// </summary>
    public void Apply(AlarmHostItemResult result)
    {
        HasResult = true;
        Value = result.Value;
        MesValue = result.MesValue;
        IsPass = result.IsPass;
        Message = result.Value.Length == 0 ? result.Message : result.Value + ", " + result.Message;
    }

    /// <summary>
    /// 通信中断不是已完成的不合格判定，清空未完成项目并保留中止原因
    /// </summary>
    public void Interrupt(string message)
    {
        Clear("已中止");
        Message = message;
    }
}
