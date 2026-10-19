using System.Globalization;
using System.Runtime.CompilerServices;
using Prism.Mvvm;

namespace IMDP.Products.GasDG.AlarmHost.Models;

/// <summary>
/// 方案页面的编辑草稿，不是持久化实体或设备执行快照。
/// 数字保留原始输入，避免 WPF 数值转换失败后模型仍保留旧值。
/// </summary>
public sealed class AlarmHostConfigDraft : BindableBase
{
    private string _productCode = "", _customerCode = "";
    private string _stationCount = "4";
    private string _meterNumberMesCode = "", _meterTimeMesCode = "", _productionStatusMesCode = "",
        _hasValveMesCode = "", _lifetimeMesCode = "", _alarmThresholdMesCode = "", _alarmRecoveryThresholdMesCode = "",
        _mainServerMesCode = "", _mainNetworkMesCode = "", _mainPowerReportMesCode = "", _imeiMesCode = "",
        _imsiMesCode = "", _iccidMesCode = "", _csqMesCode = "", _versionMesCode = "",
        _bluetoothSnOrSparkMacMesCode = "", _bluetoothBroadcastMesCode = "";
    private bool _enableMeterNumber, _enableMeterTime, _enableProductionStatus, _enableHasValve,
        _enableLifetime, _enableAlarmThreshold, _enableAlarmRecoveryThreshold, _enableMainServer,
        _enableMainNetwork, _enableMainPowerReport, _enableImei, _enableImsi, _enableIccid,
        _enableCsq, _enableVersion, _enableBluetoothSnOrSparkMac, _enableBluetoothBroadcast;
    private byte? _productionStatus, _addressType, _reportType;
    private bool _hasValve;
    private string _lifetimeSeconds = "", _alarmThreshold = "", _alarmRecoveryThreshold = "",
        _serverAddress = "", _serverPort = "", _networkUserName = "", _networkPassword = "",
        _networkApn = "", _reportCycle = "", _reportDay = "", _reportHour = "", _reportMinute = "",
        _slot1 = "", _slot2 = "", _slot3 = "", _slot4 = "", _slot5 = "";

    public string ProductCode { get => _productCode; set => SetProperty(ref _productCode, value); }
    public string CustomerCode { get => _customerCode; set => SetProperty(ref _customerCode, value); }
    public string StationCount { get => _stationCount; set => SetProperty(ref _stationCount, value); }
    // MES 项目码属于平台方案的编辑字段，不预填未经核实的现场码，不在本阶段上传。
    public string MeterNumberMesCode { get => _meterNumberMesCode; set => SetProperty(ref _meterNumberMesCode, value); }
    public string MeterTimeMesCode { get => _meterTimeMesCode; set => SetProperty(ref _meterTimeMesCode, value); }
    public string ProductionStatusMesCode { get => _productionStatusMesCode; set => SetProperty(ref _productionStatusMesCode, value); }
    public string HasValveMesCode { get => _hasValveMesCode; set => SetProperty(ref _hasValveMesCode, value); }
    public string LifetimeMesCode { get => _lifetimeMesCode; set => SetProperty(ref _lifetimeMesCode, value); }
    public string AlarmThresholdMesCode { get => _alarmThresholdMesCode; set => SetProperty(ref _alarmThresholdMesCode, value); }
    public string AlarmRecoveryThresholdMesCode { get => _alarmRecoveryThresholdMesCode; set => SetProperty(ref _alarmRecoveryThresholdMesCode, value); }
    public string MainServerMesCode { get => _mainServerMesCode; set => SetProperty(ref _mainServerMesCode, value); }
    public string MainNetworkMesCode { get => _mainNetworkMesCode; set => SetProperty(ref _mainNetworkMesCode, value); }
    public string MainPowerReportMesCode { get => _mainPowerReportMesCode; set => SetProperty(ref _mainPowerReportMesCode, value); }
    public string ImeiMesCode { get => _imeiMesCode; set => SetProperty(ref _imeiMesCode, value); }
    public string ImsiMesCode { get => _imsiMesCode; set => SetProperty(ref _imsiMesCode, value); }
    public string IccidMesCode { get => _iccidMesCode; set => SetProperty(ref _iccidMesCode, value); }
    public string CsqMesCode { get => _csqMesCode; set => SetProperty(ref _csqMesCode, value); }
    public string VersionMesCode { get => _versionMesCode; set => SetProperty(ref _versionMesCode, value); }
    public string BluetoothSnOrSparkMacMesCode { get => _bluetoothSnOrSparkMacMesCode; set => SetProperty(ref _bluetoothSnOrSparkMacMesCode, value); }
    public string BluetoothBroadcastMesCode { get => _bluetoothBroadcastMesCode; set => SetProperty(ref _bluetoothBroadcastMesCode, value); }
    public bool EnableMeterNumber { get => _enableMeterNumber; set => SetEnabled(ref _enableMeterNumber, value); }
    public bool EnableMeterTime { get => _enableMeterTime; set => SetEnabled(ref _enableMeterTime, value); }
    public bool EnableProductionStatus { get => _enableProductionStatus; set => SetEnabled(ref _enableProductionStatus, value); }
    public bool EnableHasValve { get => _enableHasValve; set => SetEnabled(ref _enableHasValve, value); }
    public bool EnableLifetime { get => _enableLifetime; set => SetEnabled(ref _enableLifetime, value); }
    public bool EnableAlarmThreshold { get => _enableAlarmThreshold; set => SetEnabled(ref _enableAlarmThreshold, value); }
    public bool EnableAlarmRecoveryThreshold { get => _enableAlarmRecoveryThreshold; set => SetEnabled(ref _enableAlarmRecoveryThreshold, value); }
    public bool EnableMainServer { get => _enableMainServer; set => SetEnabled(ref _enableMainServer, value); }
    public bool EnableMainNetwork { get => _enableMainNetwork; set => SetEnabled(ref _enableMainNetwork, value); }
    public bool EnableMainPowerReport { get => _enableMainPowerReport; set => SetEnabled(ref _enableMainPowerReport, value); }
    public bool EnableImei { get => _enableImei; set => SetEnabled(ref _enableImei, value); }
    public bool EnableImsi { get => _enableImsi; set => SetEnabled(ref _enableImsi, value); }
    public bool EnableIccid { get => _enableIccid; set => SetEnabled(ref _enableIccid, value); }
    public bool EnableCsq { get => _enableCsq; set => SetEnabled(ref _enableCsq, value); }
    public bool EnableVersion { get => _enableVersion; set => SetEnabled(ref _enableVersion, value); }
    public bool EnableBluetoothSnOrSparkMac { get => _enableBluetoothSnOrSparkMac; set => SetEnabled(ref _enableBluetoothSnOrSparkMac, value); }
    public bool EnableBluetoothBroadcast { get => _enableBluetoothBroadcast; set => SetEnabled(ref _enableBluetoothBroadcast, value); }

    public byte? ProductionStatus { get => _productionStatus; set => SetProperty(ref _productionStatus, value); }
    public bool HasValve { get => _hasValve; set => SetProperty(ref _hasValve, value); }
    public string LifetimeSeconds { get => _lifetimeSeconds; set => SetProperty(ref _lifetimeSeconds, value); }
    public string AlarmThreshold { get => _alarmThreshold; set => SetProperty(ref _alarmThreshold, value); }
    public string AlarmRecoveryThreshold { get => _alarmRecoveryThreshold; set => SetProperty(ref _alarmRecoveryThreshold, value); }
    public byte? AddressType { get => _addressType; set => SetProperty(ref _addressType, value); }
    public string ServerAddress { get => _serverAddress; set => SetProperty(ref _serverAddress, value); }
    public string ServerPort { get => _serverPort; set => SetProperty(ref _serverPort, value); }
    public string NetworkUserName { get => _networkUserName; set => SetProperty(ref _networkUserName, value); }
    public string NetworkPassword { get => _networkPassword; set => SetProperty(ref _networkPassword, value); }
    public string NetworkApn { get => _networkApn; set => SetProperty(ref _networkApn, value); }
    public string ReportCycle { get => _reportCycle; set => SetProperty(ref _reportCycle, value); }
    public string ReportDay { get => _reportDay; set => SetProperty(ref _reportDay, value); }
    public string ReportHour { get => _reportHour; set => SetProperty(ref _reportHour, value); }
    public string ReportMinute { get => _reportMinute; set => SetProperty(ref _reportMinute, value); }
    public string Slot1 { get => _slot1; set => SetProperty(ref _slot1, value); }
    public string Slot2 { get => _slot2; set => SetProperty(ref _slot2, value); }
    public string Slot3 { get => _slot3; set => SetProperty(ref _slot3, value); }
    public string Slot4 { get => _slot4; set => SetProperty(ref _slot4, value); }
    public string Slot5 { get => _slot5; set => SetProperty(ref _slot5, value); }

    public byte? ReportType
    {
        get => _reportType;
        set
        {
            if (!SetProperty(ref _reportType, value)) return;
            RaisePropertyChanged(nameof(UsesSpecifiedSchedule));
            RaisePropertyChanged(nameof(UsesOrdinarySchedule));
            RaisePropertyChanged(nameof(ReportHint));
        }
    }

    public bool UsesSpecifiedSchedule => ReportType is 7 or 8;
    public bool UsesOrdinarySchedule => !UsesSpecifiedSchedule;
    public string ReportHint => ReportType switch
    {
        7 => "指定五时：每项填写 HHmm，例如 0830。",
        8 => "指定五日：每项填写 ddHH，例如 1508。",
        _ => "普通上报：填写周期、日、时、分；切换类型保留已填值。"
    };
    public int EnabledSettingCount => new[] { EnableMeterNumber, EnableMeterTime, EnableProductionStatus,
        EnableHasValve, EnableLifetime, EnableAlarmThreshold, EnableAlarmRecoveryThreshold,
        EnableMainServer, EnableMainNetwork, EnableMainPowerReport }.Count(x => x);
    public int EnabledInspectionCount => new[] { EnableImei, EnableImsi, EnableIccid, EnableCsq,
        EnableVersion, EnableBluetoothSnOrSparkMac, EnableBluetoothBroadcast }.Count(x => x);

    private void SetEnabled(ref bool field, bool value, [CallerMemberName] string? name = null)
    {
        if (!SetProperty(ref field, value, name)) return;
        RaisePropertyChanged(nameof(EnabledSettingCount));
        RaisePropertyChanged(nameof(EnabledInspectionCount));
    }

    /// <summary>仅检查页面输入形态，设备/固件的字节长度与业务规则在协议接入阶段再校验。</summary>
    public IReadOnlyList<string> GetInputErrors()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(ProductCode)) errors.Add("请填写产品编码。");
        if (string.IsNullOrWhiteSpace(CustomerCode)) errors.Add("请填写客户编码。");
        if (!int.TryParse(StationCount?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var stations) || stations < 1)
            errors.Add("工位数量应为正整数。");
        if (EnableProductionStatus && ProductionStatus is not (0 or 1)) errors.Add("请选择生产状态。");
        if (EnableMainServer)
        {
            if (AddressType is not (0 or 1)) errors.Add("请选择服务器地址类型。");
            if (string.IsNullOrWhiteSpace(ServerAddress)) errors.Add("请填写服务器地址。");
            CheckNumber(ServerPort, ushort.MaxValue, "服务器端口");
        }
        if (EnableLifetime) CheckNumber(LifetimeSeconds, uint.MaxValue, "寿命时间");
        if (EnableAlarmThreshold) CheckNumber(AlarmThreshold, byte.MaxValue, "报警浓度阈值");
        if (EnableAlarmRecoveryThreshold) CheckNumber(AlarmRecoveryThreshold, byte.MaxValue, "报警恢复阈值");
        if (EnableMainPowerReport)
        {
            if (ReportType is not (0 or 1 or 2 or 3 or 4 or 5 or 7 or 8 or 9))
                errors.Add("请选择上报类型。");
            else if (UsesSpecifiedSchedule)
            {
                var slots = new[] { Slot1, Slot2, Slot3, Slot4, Slot5 };
                for (var i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i]?.Trim() ?? "";
                    var valid = slot.Length == 4 && slot.All(c => c is >= '0' and <= '9');
                    if (valid)
                    {
                        var first = int.Parse(slot[..2], CultureInfo.InvariantCulture);
                        var second = int.Parse(slot[2..], CultureInfo.InvariantCulture);
                        valid = ReportType == 7 ? first <= 23 && second <= 59
                            : first is >= 1 and <= 31 && second <= 23;
                    }
                    if (!valid) errors.Add($"上报时段 {i + 1} 应为有效的 {(ReportType == 7 ? "HHmm" : "ddHH")}。");
                }
            }
            else
            {
                CheckNumber(ReportCycle, byte.MaxValue, "上报周期");
                CheckNumber(ReportDay, 31, "上报日");
                CheckNumber(ReportHour, 23, "上报时");
                CheckNumber(ReportMinute, 59, "上报分");
            }
        }
        return errors;

        void CheckNumber(string value, uint max, string label)
        {
            if (!uint.TryParse(value?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number > max)
                errors.Add($"{label}应为 0–{max} 的整数。");
        }
    }
}
