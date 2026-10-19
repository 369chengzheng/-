namespace IMDP.Products.GasDG.AlarmHost.Models;

/// <summary>查询列表的只读显示形态；当前没有样例数据或仓储，后续由查询结果投影。</summary>
public sealed class AlarmHostConfigListItem
{
    public string ProductCode { get; init; } = "";
    public string CustomerCode { get; init; } = "";
    public int StationCount { get; init; }
    public string CreatedBy { get; init; } = "";
    public DateTime? CreatedTime { get; init; }
    public string UpdatedBy { get; init; } = "";
    public DateTime? UpdatedTime { get; init; } 
    public string MeterNumber { get; init; } = "";
    public string MeterTime { get; init; } = "";
    public string ProductionStatus { get; init; } = "";
    public string HasValve { get; init; } = "";
    public string Lifetime { get; init; } = "";
    public string AlarmThreshold { get; init; } = "";
    public string AlarmRecoveryThreshold { get; init; } = "";
    public string MainServer { get; init; } = "";
    public string MainNetwork { get; init; } = "";
    public string MainPowerReport { get; init; } = "";
    public string Imei { get; init; } = "";
    public string Imsi { get; init; } = "";
    public string Iccid { get; init; } = "";
    public string Csq { get; init; } = "";
    public string Version { get; init; } = "";
    public string BluetoothSnOrSparkMac { get; init; } = "";
    public string BluetoothBroadcast { get; init; } = "";
}
