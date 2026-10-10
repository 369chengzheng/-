using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Events;
using IMDP.Application.DTOs;
using IMDP.Application.Services;
using IMDP.Application.Events;
using IMDP.Domain.Products;
using IMDP.Products.GasDG.WholeA.Protocol;

namespace IMDP.Products.GasDG.WholeA.ViewModels;

public partial class WholeConfigViewModel : ObservableObject, INavigationAware
{
    private readonly ITestPlanService _testPlanService;
    private readonly IEventAggregator _eventAggregator;

    private string _planTypeKey = "WholeSet";
    private int _planTypeCode => _planTypeKey == "WholeSet" ? 0 : 1;

    [ObservableProperty]
    private string _breadCrumb = "首页 > 整机设置方案配置";

    public string CurrentPageName => _planTypeKey == "WholeSet" ? "整机设置方案配置" : "整机检验方案配置";

    /// <summary>当前软件物料号（由主界面导航时传入，用于面包屑显示，区别于配置页内输入的 MaterialNumber）</summary>
    [ObservableProperty]
    private string _currentMaterialNumber = string.Empty;

    [ObservableProperty]
    private string _productCode = string.Empty;

    [ObservableProperty]
    private string _customerCode = string.Empty;

    [ObservableProperty]
    private string _materialNumber = string.Empty;

    [ObservableProperty]
    private int _stationCount = 4;

    /// <summary>配置参数包装列表（绑定到 UI 的 ItemsControl）</summary>
    [ObservableProperty]
    private ObservableCollection<ConfigParameter> _parameters = new();

    /// <summary>抄表时间参数组（主参数 + 子参数，用于视觉分组展示）</summary>
    [ObservableProperty]
    private ObservableCollection<ConfigParameter> _readingTimeGroupParameters = new();

    /// <summary>其他参数列表（非抄表时间参数）</summary>
    [ObservableProperty]
    private ObservableCollection<ConfigParameter> _otherParameters = new();

    [ObservableProperty]
    private ObservableCollection<TestPlanDto> _plans = new();

    [ObservableProperty]
    private TestPlanDto? _selectedPlan;

    [ObservableProperty]
    private int _pageIndex = 1;

    [ObservableProperty]
    private int _pageSize = 10;

    [ObservableProperty]
    private int _totalCount;

    public IRelayCommand QueryCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand DeleteCommand { get; }
    public IRelayCommand GoHomeCommand { get; }

    public WholeConfigViewModel(ITestPlanService testPlanService, IEventAggregator eventAggregator)
    {
        _testPlanService = testPlanService;
        _eventAggregator = eventAggregator;

        QueryCommand = new RelayCommand(OnQuery);
        SaveCommand = new RelayCommand(OnSave);
        DeleteCommand = new RelayCommand(OnDelete);
        GoHomeCommand = new RelayCommand(OnGoHome);

        InitializeWholeSetParameters();
    }

    private void OnGoHome()
    {
        _eventAggregator.GetEvent<GoHomeEvent>().Publish();
    }

    /// <summary>
    /// 创建单值检测项目 DTO 的辅助方法
    /// <para>构造一个 <see cref="TestParameterDto"/>，并通过子参数（<see cref="ParasContentDto"/>）承载主值与可选上下限。</para>
    /// <para>子参数属性填入规则：</para>
    /// <para>1. <see cref="ParasContentDto.Code"/>：取 <see cref="SubParameterCodes"/> 常量（VALUE/LOWER_LIMIT/UPPER_LIMIT）作为角色标识；</para>
    /// <para>2. <see cref="ParasContentDto.Value"/>：主值取方法参数 <paramref name="value"/>，上下限取 <paramref name="lowerLimit"/>/<paramref name="upperLimit"/> 的字符串形式；</para>
    /// <para>3. <see cref="ParasContentDto.Order"/>：主值=1、下限=2、上限=3，决定子参数在父项目内的显示顺序；</para>
    /// <para>4. <see cref="ParasContentDto.Name"/>：取 <see cref="SubParameterCodes"/> 中对应的显示名常量（"值"/"下限"/"上限"）；</para>
    /// <para>5. <see cref="ParasContentDto.ControlType"/>：取方法参数 <paramref name="controlType"/>；为 null 时按规则自动推断（见 <paramref name="controlType"/> 参数说明）；</para>
    /// <para>6. <see cref="ParasContentDto.DefaultValue"/>：取方法参数 <paramref name="defaultValue"/>，为 null 时回退为 <paramref name="value"/>；</para>
    /// <para>7. <see cref="ParasContentDto.Validator"/>：取方法参数 <paramref name="validator"/>；为 null 但存在上下限时，自动生成范围校验器。</para>
    /// <para>父项目维度：Code=<paramref name="code"/>（协议变量名，用于与 View 绑定），MesCode 默认同 Code，Name=<paramref name="name"/>（中文显示名），Order=<paramref name="order"/>。</para>
    /// </summary>
    /// <param name="name">检测项目显示名称（如"表具底码"），对应 <see cref="TestParameterDto.Name"/></param>
    /// <param name="order">检测项目排序序号，对应 <see cref="TestParameterDto.Order"/></param>
    /// <param name="code">检测项目编码（协议变量名，如 BASE_CODE），对应 <see cref="TestParameterDto.Code"/></param>
    /// <param name="value">主值字符串（对应 Code=<see cref="SubParameterCodes.Value"/> 的子参数）</param>
    /// <param name="lowerLimit">可选下限值（为 null 时不创建下限子参数）</param>
    /// <param name="upperLimit">可选上限值（为 null 时不创建上限子参数）</param>
    /// <param name="isEnabled">是否启用该检测项目</param>
    /// <param name="controlType">子参数控件类型；为 null 时自动推断：
    /// <para>- 枚举参数（<see cref="ProtocolEnumOptions.IsEnumParameter"/> 返回 true）→ <see cref="ParameterControlType.ComboBox"/>；</para>
    /// <para>- 存在上下限 → <see cref="ParameterControlType.NumericUpDown"/>；</para>
    /// <para>- 其他 → <see cref="ParameterControlType.TextBox"/>。</para>
    /// </param>
    /// <param name="defaultValue">子参数默认值（为 null 时使用 <paramref name="value"/> 作为默认值）</param>
    /// <param name="validator">子参数值校验规则（为 null 且存在上下限时，自动生成范围校验器）</param>
    /// <returns>构造完成的 <see cref="TestParameterDto"/></returns>
    internal static TestParameterDto P(string name, int order, string code,
        string value = "", double? lowerLimit = null, double? upperLimit = null, bool isEnabled = true,
        ParameterControlType? controlType = null,
        string? defaultValue = null,
        ParameterValidator? validator = null)
    {
        // 控件类型自动推断：枚举参数 → ComboBox；有上下限 → NumericUpDown；其他 → TextBox
        var effectiveControlType = controlType ??
            (ProtocolEnumOptions.IsEnumParameter(code)
                ? ParameterControlType.ComboBox
                : (lowerLimit.HasValue || upperLimit.HasValue
                    ? ParameterControlType.NumericUpDown
                    : ParameterControlType.TextBox));

        // 若未显式传入校验规则，但存在上下限，则自动生成范围校验器
        var effectiveValidator = validator ?? ((lowerLimit.HasValue || upperLimit.HasValue)
            ? new ParameterValidator { MinValue = lowerLimit, MaxValue = upperLimit }
            : null);

        var parameters = new List<ParasContentDto>
        {
            new()
            {
                Name = SubParameterCodes.ValueDisplayName,
                Code = SubParameterCodes.Value,
                Value = value,
                Order = SubParameterCodes.ValueOrder,
                ControlType = effectiveControlType,
                DefaultValue = defaultValue ?? value,
                Validator = effectiveValidator
            }
        };
        if (lowerLimit.HasValue)
            parameters.Add(new ParasContentDto { Name = SubParameterCodes.LowerLimitDisplayName, Code = SubParameterCodes.LowerLimit, Value = lowerLimit.Value.ToString(), Order = SubParameterCodes.LowerLimitOrder });
        if (upperLimit.HasValue)
            parameters.Add(new ParasContentDto { Name = SubParameterCodes.UpperLimitDisplayName, Code = SubParameterCodes.UpperLimit, Value = upperLimit.Value.ToString(), Order = SubParameterCodes.UpperLimitOrder });

        return new TestParameterDto
        {
            Name = name,
            Order = order,
            Code = code,
            MesCode = code,
            IsEnabled = isEnabled,
            Parameters = parameters
        };
    }
    /// <summary>
    /// 初始化整机设置方案参数（35 项写参数）
    /// <para>协议 4.1.1"写出厂配置参数"（数据标识 0xC518，控制码 0x04）</para>
    /// <para>合并参数：月冻结(FREEZE_DATETIME)、域名端口(DOMAIN_ADDR)、IP地址端口(IP_ADDR_PORT)、网络参数(NET_AUTH)</para>
    /// </summary>
    private void InitializeWholeSetParameters()
    {
        var dtos = BuildWriteParameterDtos();
        ApplyParametersToUI(dtos);
    }

    /// <summary>
    /// 初始化整机检验方案参数（写参数 + 读参数）
    /// <para>写参数：协议 4.1.1"写出厂配置参数"（0xC518），与整机设置方案共用同一套参数定义</para>
    /// <para>读参数：协议 4.1.2"读出厂配置参数"（0xC518）、4.4"网络参数"（0xC525）、4.5"设备运行信息"（0xC527）</para>
    /// </summary>
    private void InitializeWholeGetParameters()
    {
        var dtos = BuildWriteParameterDtos();
        dtos.AddRange(BuildReadParameterDtos(dtos.Count + 1));
        ApplyParametersToUI(dtos);
    }

    /// <summary>
    /// 构建整机写参数 DTO 列表（35 项，含 4 个合并父参数）
    /// <para>严格遵循协议 4.1.1"写出厂配置参数"（数据标识 0xC518，控制码 0x04）</para>
    /// <para>合并参数：月冻结(FREEZE_DATETIME)、域名端口(DOMAIN_ADDR)、IP地址端口(IP_ADDR_PORT)、网络参数(NET_AUTH)</para>
    /// <para>该方法集中定义写参数，供 <see cref="InitializeWholeSetParameters"/> 和 <see cref="InitializeWholeGetParameters"/> 复用，消除两者间的重复代码。</para>
    /// </summary>
    /// <param name="startOrder">起始排序序号（默认 1，对应整机设置方案）</param>
    /// <returns>35 个写参数 DTO 列表，Order 从 <paramref name="startOrder"/> 开始连续递增</returns>
    internal static List<TestParameterDto> BuildWriteParameterDtos(int startOrder = 1)
    {
        var order = startOrder;
        var dtos = new List<TestParameterDto>();

        // ===== 基础配置参数（7 项） =====
        dtos.Add(P("是否恢复出厂设置", order++, "RESET_FACTORY", "0"));
        // 表具底码：协议 4.1.1 4B HEX，2 位小数，范围 0~999999.99
        dtos.Add(P("表具底码", order++, "BASE_CODE", "0.00",
            validator: new ParameterValidator { MinValue = 0, MaxValue = 999999.99, ErrorMessage = "表具底码应在 0~999999.99 之间" }));
        dtos.Add(P("运行模式", order++, "RUN_MODE", "0"));
        // 预充金额/排空量：协议 4.1.1 4B HEX，2 位小数，范围 0~999999.99
        dtos.Add(P("预充金额/排空量", order++, "PREPAID_AMT", "0.00",
            validator: new ParameterValidator { MinValue = 0, MaxValue = 999999.99, ErrorMessage = "预充金额/排空量应在 0~999999.99 之间" }));
        // 表存上限：协议 4.1.1 4B HEX，2 位小数，范围 0~999999.99
        dtos.Add(P("表存上限", order++, "STORAGE_LIMIT", "999999.99",
            validator: new ParameterValidator { MinValue = 0, MaxValue = 999999.99, ErrorMessage = "表存上限应在 0~999999.99 之间" }));
        dtos.Add(P("表计费类型", order++, "BILLING_TYPE", "0"));
        dtos.Add(P("表具规格", order++, "METER_SPEC", "0x03"));

        // ===== 抄表时间参数组（12 项）：父参数 + 11 个子参数（动态日/时输入） =====
        // 协议 4.1.4：抄表日字段为 10B，含 周期1B + 日/时/分 3BCD + 6B 0xFF
        // 0x07 指定5时：10B = 5 个时间（2B BCD × 5）
        // 0x08 指定5日：10B = 5 个日期（2B BCD × 5）
        dtos.Add(P("抄表时间", order++, "READING_TYPE", "0"));
        dtos.Add(P("抄表日(日 时 分)", order++, "READING_DAY", "28 00 00", isEnabled: false));
        dtos.Add(P("抄表时1", order++, "READING_TIME_1", "01 00", isEnabled: false));
        dtos.Add(P("抄表日1", order++, "READING_DATE_1", "01 00", isEnabled: false));
        dtos.Add(P("抄表时2", order++, "READING_TIME_2", "02 30", isEnabled: false));
        dtos.Add(P("抄表日2", order++, "READING_DATE_2", "02 08", isEnabled: false));
        dtos.Add(P("抄表时3", order++, "READING_TIME_3", "01 00", isEnabled: false));
        dtos.Add(P("抄表日3", order++, "READING_DATE_3", "01 00", isEnabled: false));
        dtos.Add(P("抄表时4", order++, "READING_TIME_4", "02 30", isEnabled: false));
        dtos.Add(P("抄表日4", order++, "READING_DATE_4", "02 08", isEnabled: false));
        dtos.Add(P("抄表时5", order++, "READING_TIME_5", "01 00", isEnabled: false));
        dtos.Add(P("抄表日5", order++, "READING_DATE_5", "02 08", isEnabled: false));

        // ===== 月冻结（合并参数）：父参数 + 3 个子参数（日/时/分） =====
        // 协议 4.1.7：FREEZE_DAY 0~28（0=天冻结）、FREEZE_HOUR 0~23、FREEZE_MINUTE 0~59
        dtos.Add(P("月冻结", order++, "FREEZE_DATETIME", ""));
        dtos.Add(P("日", order++, "FREEZE_DAY", "28", isEnabled: false,
            validator: new ParameterValidator { MinValue = 0, MaxValue = 28, ErrorMessage = "月冻结日应在 0~28 之间（0=天冻结）" }));
        dtos.Add(P("时", order++, "FREEZE_HOUR", "0", isEnabled: false,
            validator: new ParameterValidator { MinValue = 0, MaxValue = 23, ErrorMessage = "月冻结时应在 0~23 之间" }));
        dtos.Add(P("分", order++, "FREEZE_MINUTE", "0", isEnabled: false,
            validator: new ParameterValidator { MinValue = 0, MaxValue = 59, ErrorMessage = "月冻结分应在 0~59 之间" }));

        // ===== 域名端口（合并参数）：父参数 + 2 个子参数（域名/端口） =====
        // 协议 4.1.1：DOMAIN ASCII 最长 30 字节，DOMAIN_PORT 2B HEX 0~65535
        dtos.Add(P("域名端口", order++, "DOMAIN_ADDR", ""));
        dtos.Add(P("域名", order++, "DOMAIN", "wlw.viewshine.cn", isEnabled: false,
            validator: new ParameterValidator { MaxLength = 30, ErrorMessage = "域名最长 30 字节" }));
        dtos.Add(P("端口", order++, "DOMAIN_PORT", "8900", isEnabled: false,
            validator: new ParameterValidator { MinValue = 0, MaxValue = 65535, ErrorMessage = "端口应在 0~65535 之间" }));

        // ===== IP地址端口（合并参数）：父参数 + 2 个子参数（IP地址/IP端口） =====
        // 协议 4.1.6：IP_ADDR IPv4 格式，IP_PORT 2B HEX 0~65535
        dtos.Add(P("IP地址端口", order++, "IP_ADDR_PORT", ""));
        dtos.Add(P("IP地址", order++, "IP_ADDR", "192.168.1.1", isEnabled: false,
            validator: new ParameterValidator { RegexPattern = @"^(\d{1,3}\.){3}\d{1,3}$", ErrorMessage = "IP地址格式应为 x.x.x.x" }));
        dtos.Add(P("IP端口", order++, "IP_PORT", "8888", isEnabled: false,
            validator: new ParameterValidator { MinValue = 0, MaxValue = 65535, ErrorMessage = "IP端口应在 0~65535 之间" }));

        // ===== 网络参数（合并参数）：父参数 + 2 个子参数（用户名/密码） =====
        // 协议 4.1.5：NET_USER/NET_pwd ASCII 最长 30 字节
        dtos.Add(P("网络参数", order++, "NET_AUTH", ""));
        dtos.Add(P("用户名", order++, "NET_USER", "administrator", isEnabled: false,
            validator: new ParameterValidator { MaxLength = 30, ErrorMessage = "用户名最长 30 字节" }));
        dtos.Add(P("密码", order++, "NET_PWD", "123456", isEnabled: false,
            validator: new ParameterValidator { MaxLength = 30, ErrorMessage = "密码最长 30 字节" }));

        // ===== 其他配置参数（3 项） =====
        dtos.Add(P("信息安全加密", order++, "ENCRYPT", "1"));
        dtos.Add(P("唤醒周期生效", order++, "WAKE_CYCLE", "1"));
        dtos.Add(P("状态配置-是否有阀门", order++, "STATUS_VALVE", "0"));

        return dtos;
    }

    /// <summary>
    /// 构建整机读参数 DTO 列表（43 项）
    /// <para>协议 4.1.2"读出厂配置参数"（0xC518）、4.4"网络参数"（0xC525）、4.5"设备运行信息"（0xC527）</para>
    /// <para>该方法集中定义读参数，供 <see cref="InitializeWholeGetParameters"/> 调用，Order 紧接写参数之后。</para>
    /// </summary>
    /// <param name="startOrder">起始排序序号（应紧接写参数的最后一个 Order + 1）</param>
    /// <returns>43 个读参数 DTO 列表，Order 从 <paramref name="startOrder"/> 开始连续递增</returns>
    internal static List<TestParameterDto> BuildReadParameterDtos(int startOrder)
    {
        var order = startOrder;
        var dtos = new List<TestParameterDto>();

        // ===== 协议 4.1.2：模拟量与状态参数（35 项） =====
        // 模拟量参数（9 项）：带上下限的数值参数，由 P 方法自动推断为 NumericUpDown 控件
        dtos.Add(P("通讯电池电压(V)", order++, "COMM_BATT_V", lowerLimit: 3.6, upperLimit: 4.5));
        dtos.Add(P("计量电池电压(V)", order++, "MEAS_BATT_V", lowerLimit: 3.6, upperLimit: 4.5));
        dtos.Add(P("信号强度", order++, "SIGNAL_STR", lowerLimit: 10, upperLimit: 49));
        dtos.Add(P("温度值(°C)", order++, "TEMP", lowerLimit: -40, upperLimit: 85));
        dtos.Add(P("压力值", order++, "PRESSURE", lowerLimit: 0, upperLimit: 100));
        dtos.Add(P("报警器浓度(%LEL)", order++, "ALARM_CONC", lowerLimit: 0, upperLimit: 100));
        dtos.Add(P("CSQ值", order++, "CSQ", lowerLimit: 10, upperLimit: 31));
        dtos.Add(P("RSRP值(dBm)", order++, "RSRP", lowerLimit: -130, upperLimit: -44));
        dtos.Add(P("SNR值(dB)", order++, "SNR", lowerLimit: -20, upperLimit: 30));
        // 状态参数（26 项）：协议定义的状态码，默认值为 "0"/"1"/"001" 等
        dtos.Add(P("GPRS模块状态", order++, "GPRS_STATUS", "1"));
        dtos.Add(P("TCP连接状态", order++, "TCP_STATUS", "1"));
        dtos.Add(P("SIM卡状态", order++, "SIM_STATUS", "1"));
        dtos.Add(P("阀门状态", order++, "VALVE_STATUS", "001"));
        dtos.Add(P("报警器状态", order++, "ALARM_STATUS", "1"));
        dtos.Add(P("RS485接口状态", order++, "RS485_STATUS", "1"));
        dtos.Add(P("蓝牙连接状态", order++, "BT_STATUS", "1"));
        dtos.Add(P("通讯类型", order++, "COMM_TYPE", "0"));
        dtos.Add(P("表具类型", order++, "METER_TYPE", "0"));
        dtos.Add(P("安全表是否加密", order++, "IS_ENCRYPTED", "1"));
        dtos.Add(P("拆表报警", order++, "TAMPER", "0"));
        dtos.Add(P("大流报警", order++, "HIGH_FLOW", "0"));
        dtos.Add(P("恒流报警", order++, "CONST_FLOW", "0"));
        dtos.Add(P("小流报警", order++, "LOW_FLOW", "0"));
        dtos.Add(P("计量报警", order++, "MEAS_ALARM", "0"));
        dtos.Add(P("闲置报警", order++, "IDLE_ALARM", "0"));
        dtos.Add(P("反向报警", order++, "REVERSE_ALARM", "0"));
        dtos.Add(P("电压状态", order++, "VOLTAGE_STATUS", "0"));
        dtos.Add(P("压力失压", order++, "PRESSURE_LOSS", "0"));
        dtos.Add(P("压力超压", order++, "PRESSURE_OVER", "0"));
        dtos.Add(P("压力传感器", order++, "PRESSURE_SENSOR", "0"));
        dtos.Add(P("温度传感器", order++, "TEMP_SENSOR", "0"));
        dtos.Add(P("开盖报警", order++, "OPEN_COVER", "0"));
        dtos.Add(P("燃气泄漏", order++, "GAS_LEAK", "0"));
        dtos.Add(P("日用气量上限", order++, "DAILY_LIMIT", "0"));
        dtos.Add(P("失联报警", order++, "DISCONNECT", "0"));

        // ===== 协议 4.5：设备运行信息数值参数（3 项） =====
        dtos.Add(P("剩余金额", order++, "REMAINING_AMT", lowerLimit: 0, upperLimit: 999999.99));
        dtos.Add(P("单价", order++, "UNIT_PRICE", lowerLimit: 0, upperLimit: 9999.9999));
        dtos.Add(P("ECL信号等级", order++, "ECL", lowerLimit: 0, upperLimit: 6));

        // ===== 协议 4.4：网络参数定长字符串（5 项） =====
        // IMEI/IMSI/ICCID/BT_SN 为定长 ASCII 字符串，长度上限由协议规定
        dtos.Add(P("IMEI号", order++, "IMEI", "",
            validator: new ParameterValidator { MaxLength = 15, ErrorMessage = "IMEI 号应为 15 位 ASCII 字符" }));
        dtos.Add(P("IMSI号", order++, "IMSI", "",
            validator: new ParameterValidator { MaxLength = 20, ErrorMessage = "IMSI 号最长 20 位 ASCII 字符" }));
        dtos.Add(P("ICCID号", order++, "ICCID", "",
            validator: new ParameterValidator { MaxLength = 20, ErrorMessage = "ICCID 号最长 20 位 ASCII 字符" }));
        dtos.Add(P("蓝牙模块SN号", order++, "BT_SN", "",
            validator: new ParameterValidator { MaxLength = 12, ErrorMessage = "蓝牙模块 SN 号应为 12 位 ASCII 字符" }));
        dtos.Add(P("温度传感器类型", order++, "TEMP_SENSOR_TYPE", "0x00"));

        return dtos;
    }

    /// <summary>
    /// 将参数 DTO 列表应用到 UI 绑定集合
    /// <para>统一执行 5 步 UI 初始化逻辑，消除 <see cref="InitializeWholeSetParameters"/>、
    /// <see cref="InitializeWholeGetParameters"/>、<see cref="OnSelectedPlanChanged"/> 三处的重复代码：</para>
    /// <para>1. 包装为 <see cref="ConfigParameter"/> 并赋值给 <see cref="Parameters"/>；</para>
    /// <para>2. 重建合并参数的父子关系（<see cref="RebuildMergedParameterLinks"/>）；</para>
    /// <para>3. 初始化 <see cref="OtherParameters"/>（排除合并子参数，避免重复展示）；</para>
    /// <para>4. 订阅参数值变化事件（<see cref="SubscribeParameterValueChanged"/>）；</para>
    /// <para>5. 根据默认抄表类型值应用可见性规则（<see cref="OnReadingTypeChanged"/>）。</para>
    /// </summary>
    /// <param name="dtos">参数 DTO 列表（来自 <see cref="BuildWriteParameterDtos"/> 或已保存方案的 Parameters）</param>
    private void ApplyParametersToUI(List<TestParameterDto> dtos)
    {
        // 步骤 1：包装为 ConfigParameter 并赋值给 Parameters
        Parameters = new ObservableCollection<ConfigParameter>(dtos.Select(d => new ConfigParameter(d)));

        // 步骤 2：重建合并参数的父子关系（抄表时间子参数也纳入合并参数体系）
        RebuildMergedParameterLinks();

        // 步骤 3：所有参数统一展示（合并子参数在父参数卡片内展示，不单独显示）
        OtherParameters = new ObservableCollection<ConfigParameter>(
            Parameters.Where(p => !MergedParameterMap.IsChild(p.Code)));
        ReadingTimeGroupParameters = new ObservableCollection<ConfigParameter>();

        // 步骤 4：订阅参数值变化事件，实现抄表时间联动
        SubscribeParameterValueChanged();

        // 步骤 5：根据默认抄表类型值应用可见性规则
        var defaultReadingType = Parameters.FirstOrDefault(p => p.Code == "READING_TYPE")?.Value;
        OnReadingTypeChanged(defaultReadingType);
    }

    /// <summary>
    /// 重建合并参数的父子关系
    /// <para>从扁平 Parameters 列表中查找合并父参数和子参数，将子参数添加到父参数的 SubParameters 集合</para>
    /// <para>用于初始化和加载已保存方案后重建 UI 展示所需的父子结构</para>
    /// </summary>
    private void RebuildMergedParameterLinks()
    {
        // 按 Order 排序的子参数分组到对应父参数
        var parentParams = Parameters.Where(p => MergedParameterMap.IsParent(p.Code)).ToList();
        foreach (var parent in parentParams)
        {
            parent.IsMergedParent = true;
            parent.SubParameters.Clear();
            var childCodes = MergedParameterMap.ChildToParent
                .Where(kv => kv.Value == parent.Code)
                .Select(kv => kv.Key)
                .ToHashSet();
            var children = Parameters
                .Where(p => childCodes.Contains(p.Code))
                .OrderBy(p => p.Order);
            foreach (var child in children)
            {
                child.IsChildParameter = true;
                parent.SubParameters.Add(child);
            }
        }
    }

    /// <summary>
    /// 参数值变化事件处理
    /// </summary>
    private void OnParameterValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (e.Code == "READING_TYPE")
        {
            OnReadingTypeChanged(e.NewValue);
        }
    }

    partial void OnSelectedPlanChanged(TestPlanDto? value)
    {
        if (value != null)
        {
            ProductCode = value.ProductCode;
            CustomerCode = value.CustomerCode;
            MaterialNumber = value.MaterialNumber;
            StationCount = value.StationCount;

            // 数据迁移：补全旧数据中缺失的 Code 字段，确保合并参数父子关系可识别
            NormalizeLoadedParameters(value.Parameters, _planTypeKey == "WholeSet");

            // 加载已保存方案后应用参数到 UI（复用统一的 5 步初始化逻辑）
            ApplyParametersToUI(value.Parameters);
        }
    }

    /// <summary>
    /// 规范化已加载的参数列表，补全旧数据中缺失的 Code 字段
    /// <para>历史数据兼容：旧版本 TestParameterDto 没有 Code 字段，加载后 Code 为空。</para>
    /// <para>通过 MesCode 或 Name 反向匹配协议定义的 Code，使 MergedParameterMap 能识别父子关系。</para>
    /// </summary>
    private static void NormalizeLoadedParameters(List<TestParameterDto> parameters, bool isWholeSet)
    {
        // 构建 MesCode → Code 的反向映射表（基于协议定义的参数集）
        var mesCodeToCode = BuildMesCodeToCodeMap(isWholeSet);

        foreach (var dto in parameters)
        {
            // 若 Code 已有值，跳过
            if (!string.IsNullOrEmpty(dto.Code)) continue;

            // 优先通过 MesCode 匹配（旧数据中 MesCode 通常等于 Code）
            if (!string.IsNullOrEmpty(dto.MesCode) && mesCodeToCode.TryGetValue(dto.MesCode, out var codeByMes))
            {
                dto.Code = codeByMes;
                continue;
            }

            // 回退通过 Name 匹配
            if (!string.IsNullOrEmpty(dto.Name))
            {
                var directCode = GetCodeByName(dto.Name, isWholeSet);
                if (!string.IsNullOrEmpty(directCode))
                {
                    dto.Code = directCode;
                }
            }
        }
    }

    /// <summary>构建 MesCode → Code 映射表（协议定义的参数集）</summary>
    private static Dictionary<string, string> BuildMesCodeToCodeMap(bool isWholeSet)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // WholeSet 和 WholeGet 共用的写参数
        AddParam(map, "是否恢复出厂设置", "RESET_FACTORY");
        AddParam(map, "表具底码", "BASE_CODE");
        AddParam(map, "运行模式", "RUN_MODE");
        AddParam(map, "预充金额/排空量", "PREPAID_AMT");
        AddParam(map, "表存上限", "STORAGE_LIMIT");
        AddParam(map, "表计费类型", "BILLING_TYPE");
        AddParam(map, "表具规格", "METER_SPEC");
        AddParam(map, "抄表时间", "READING_TYPE");
        AddParam(map, "抄表日(日 时 分)", "READING_DAY");
        AddParam(map, "抄表时1", "READING_TIME_1");
        AddParam(map, "抄表日1", "READING_DATE_1");
        AddParam(map, "抄表时2", "READING_TIME_2");
        AddParam(map, "抄表日2", "READING_DATE_2");
        AddParam(map, "抄表时3", "READING_TIME_3");
        AddParam(map, "抄表日3", "READING_DATE_3");
        AddParam(map, "抄表时4", "READING_TIME_4");
        AddParam(map, "抄表日4", "READING_DATE_4");
        AddParam(map, "抄表时5", "READING_TIME_5");
        AddParam(map, "抄表日5", "READING_DATE_5");
        AddParam(map, "月冻结", "FREEZE_DATETIME");
        AddParam(map, "日", "FREEZE_DAY");
        AddParam(map, "时", "FREEZE_HOUR");
        AddParam(map, "分", "FREEZE_MINUTE");
        AddParam(map, "域名端口", "DOMAIN_ADDR");
        AddParam(map, "域名", "DOMAIN");
        AddParam(map, "端口", "DOMAIN_PORT");
        AddParam(map, "IP地址端口", "IP_ADDR_PORT");
        AddParam(map, "IP地址", "IP_ADDR");
        AddParam(map, "IP端口", "IP_PORT");
        AddParam(map, "网络参数", "NET_AUTH");
        AddParam(map, "用户名", "NET_USER");
        AddParam(map, "密码", "NET_PWD");
        AddParam(map, "信息安全加密", "ENCRYPT");
        AddParam(map, "唤醒周期生效", "WAKE_CYCLE");
        AddParam(map, "状态配置-是否有阀门", "STATUS_VALVE");

        // WholeGet 额外的读参数
        AddParam(map, "通讯电池电压(V)", "COMM_BATT_V");
        AddParam(map, "计量电池电压(V)", "MEAS_BATT_V");
        AddParam(map, "信号强度", "SIGNAL_STR");
        AddParam(map, "温度值(°C)", "TEMP");
        AddParam(map, "压力值", "PRESSURE");
        AddParam(map, "报警器浓度(%LEL)", "ALARM_CONC");
        AddParam(map, "CSQ值", "CSQ");
        AddParam(map, "RSRP值(dBm)", "RSRP");
        AddParam(map, "SNR值(dB)", "SNR");
        AddParam(map, "GPRS模块状态", "GPRS_STATUS");
        AddParam(map, "TCP连接状态", "TCP_STATUS");
        AddParam(map, "SIM卡状态", "SIM_STATUS");
        AddParam(map, "阀门状态", "VALVE_STATUS");
        AddParam(map, "报警器状态", "ALARM_STATUS");
        AddParam(map, "RS485接口状态", "RS485_STATUS");
        AddParam(map, "蓝牙连接状态", "BT_STATUS");
        AddParam(map, "通讯类型", "COMM_TYPE");
        AddParam(map, "表具类型", "METER_TYPE");
        AddParam(map, "安全表是否加密", "IS_ENCRYPTED");
        AddParam(map, "拆表报警", "TAMPER");
        AddParam(map, "大流报警", "HIGH_FLOW");
        AddParam(map, "恒流报警", "CONST_FLOW");
        AddParam(map, "小流报警", "LOW_FLOW");
        AddParam(map, "计量报警", "MEAS_ALARM");
        AddParam(map, "闲置报警", "IDLE_ALARM");
        AddParam(map, "反向报警", "REVERSE_ALARM");
        AddParam(map, "电压状态", "VOLTAGE_STATUS");
        AddParam(map, "压力失压", "PRESSURE_LOSS");
        AddParam(map, "压力超压", "PRESSURE_OVER");
        AddParam(map, "压力传感器", "PRESSURE_SENSOR");
        AddParam(map, "温度传感器", "TEMP_SENSOR");
        AddParam(map, "开盖报警", "OPEN_COVER");
        AddParam(map, "燃气泄漏", "GAS_LEAK");
        AddParam(map, "日用气量上限", "DAILY_LIMIT");
        AddParam(map, "失联报警", "DISCONNECT");
        AddParam(map, "剩余金额", "REMAINING_AMT");
        AddParam(map, "单价", "UNIT_PRICE");
        AddParam(map, "ECL信号等级", "ECL");
        AddParam(map, "IMEI号", "IMEI");
        AddParam(map, "IMSI号", "IMSI");
        AddParam(map, "ICCID号", "ICCID");
        AddParam(map, "蓝牙模块SN号", "BT_SN");
        AddParam(map, "温度传感器类型", "TEMP_SENSOR_TYPE");

        return map;
    }

    private static void AddParam(Dictionary<string, string> map, string name, string code)
    {
        map[name] = code;
        map[code] = code; // MesCode 默认等于 Code，所以也加入映射
    }

    private static string? GetCodeByName(string name, bool isWholeSet)
    {
        var map = BuildMesCodeToCodeMap(isWholeSet);
        return map.TryGetValue(name, out var code) ? code : null;
    }

    /// <summary>
    /// 统一订阅参数值变化事件，避免重复订阅导致多次触发
    /// </summary>
    private void SubscribeParameterValueChanged()
    {
        foreach (var param in Parameters)
        {
            param.ValueChanged -= OnParameterValueChanged;
            param.ValueChanged += OnParameterValueChanged;
        }
    }

    /// <summary>
    /// 抄表类型变化时，联动显示/隐藏对应的抄表日/时输入框
    /// 协议 4.1.4 抄表时间参数：
    /// 0=每天, 1=单月, 2=双月, 3=每月, 4=按分, 5=按时, 9=按天 → 显示"抄表日"
    /// 7=指定5时 → 显示5组"抄表时"
    /// 8=指定5日 → 显示5组"抄表日"
    /// </summary>
    private void OnReadingTypeChanged(string? readingTypeValue)
    {
        if (string.IsNullOrEmpty(readingTypeValue)) return;

        var parent = Parameters.FirstOrDefault(p => p.Code == "READING_TYPE");
        if (parent == null) return;

        var subs = parent.SubParameters;
        var readingDay = subs.FirstOrDefault(p => p.Code == "READING_DAY");
        var time1 = subs.FirstOrDefault(p => p.Code == "READING_TIME_1");
        var date1 = subs.FirstOrDefault(p => p.Code == "READING_DATE_1");
        var time2 = subs.FirstOrDefault(p => p.Code == "READING_TIME_2");
        var date2 = subs.FirstOrDefault(p => p.Code == "READING_DATE_2");
        var time3 = subs.FirstOrDefault(p => p.Code == "READING_TIME_3");
        var date3 = subs.FirstOrDefault(p => p.Code == "READING_DATE_3");
        var time4 = subs.FirstOrDefault(p => p.Code == "READING_TIME_4");
        var date4 = subs.FirstOrDefault(p => p.Code == "READING_DATE_4");
        var time5 = subs.FirstOrDefault(p => p.Code == "READING_TIME_5");
        var date5 = subs.FirstOrDefault(p => p.Code == "READING_DATE_5");

        // 先隐藏所有动态子参数
        foreach (var sub in subs)
        {
            sub.IsVisible = false;
            sub.IsEnabled = false;
        }

        switch (readingTypeValue)
        {
            case "7": // 指定5时：显示5组"抄表时"
                SetVisible(time1); SetVisible(time2); SetVisible(time3);
                SetVisible(time4); SetVisible(time5);
                break;

            case "8": // 指定5日：显示5组"抄表日"
                SetVisible(date1); SetVisible(date2); SetVisible(date3);
                SetVisible(date4); SetVisible(date5);
                break;

            default: // 其他类型（0/1/2/3/4/5/9）：显示"抄表日(日 时 分)"
                SetVisible(readingDay);
                // 抄表日内容动态调整：若为空或格式不正确，自动恢复默认"日 时 分"格式
                if (readingDay != null &&
                    (string.IsNullOrWhiteSpace(readingDay.Value) || readingDay.Value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length != 3))
                {
                    readingDay.Value = "28 00 00";
                }
                break;
        }
    }

    private static void SetVisible(ConfigParameter? param)
    {
        if (param != null)
        {
            param.IsVisible = true;
            param.IsEnabled = true;
        }
    }

    private async void OnQuery()
    {
        // GasDG 产品固定路由到国内燃气整机配置仓储
        var result = await _testPlanService.GetWholeConfigsAsync(_planTypeCode, ProductCode, CustomerCode, PageIndex, PageSize, ProductCategory.GasDG);
        Plans = new ObservableCollection<TestPlanDto>(result.Items);
        TotalCount = result.TotalCount;

        // 查询后自动加载匹配方案的已配置参数
        if (result.Items.Count > 0)
        {
            // 自动选中第一个匹配的方案，触发 OnSelectedPlanChanged 加载参数
            SelectedPlan = result.Items[0];
        }
        else
        {
            // 没有匹配方案时仅清空方案列表选中项
            // 保留 Parameters/OtherParameters/ReadingTimeGroupParameters 中的默认初始化参数
            // 让用户能基于默认模板配置新方案并保存
            SelectedPlan = null;
        }
    }

    /// <summary>
    /// 保存配置，包含完整性校验：
    /// 1. 产品编码必填
    /// 2. 已启用参数的值必填
    /// 3. 有上下限的参数需校验值是否在范围内
    /// 4. 枚举参数的值必须在选项列表中
    /// </summary>
    private async void OnSave()
    {
        if (string.IsNullOrWhiteSpace(ProductCode))
        {
            MessageBox.Show("请输入产品编码", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(CustomerCode))
        {
            MessageBox.Show("请输入客户编码", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var errors = new List<string>();

        foreach (var param in Parameters)
        {
            if (!param.IsEnabled) continue;

            // 合并参数的父参数本身无值，跳过校验（其子参数单独校验）
            if (param.IsMergedParent) continue;

            // 已启用参数的值必填
            if (string.IsNullOrWhiteSpace(param.Value))
            {
                errors.Add($"[{param.Name}] 值不能为空");
                continue;
            }

            // 有上下限的参数需校验值是否在范围内
            if (param.HasLimits && double.TryParse(param.Value, out var numVal))
            {
                if (param.LowerLimit.HasValue && numVal < param.LowerLimit.Value)
                    errors.Add($"[{param.Name}] 值 {numVal} 低于下限 {param.LowerLimit.Value}");
                if (param.UpperLimit.HasValue && numVal > param.UpperLimit.Value)
                    errors.Add($"[{param.Name}] 值 {numVal} 超过上限 {param.UpperLimit.Value}");
            }

            // 枚举参数的值必须在选项列表中
            if (param.IsEnum && param.Options != null && !param.Options.Any(o => o.Key == param.Value))
            {
                errors.Add($"[{param.Name}] 值 {param.Value} 不在协议定义的枚举范围内");
            }
        }

        if (errors.Count > 0)
        {
            MessageBox.Show(
                "配置校验失败：\n" + string.Join("\n", errors),
                "校验失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var dto = new TestPlanDto
        {
            ProductCode = ProductCode,
            CustomerCode = CustomerCode,
            MaterialNumber = MaterialNumber,
            PlanType = _planTypeCode,
            StationCount = StationCount,
            Parameters = Parameters.Select(p => p.ToDto()).ToList(),
            UpdatedBy = "system",
            UpdatedTime = DateTime.Now,
            Category = ProductCategory.GasDG
        };

        await _testPlanService.SaveWholeConfigAsync(dto, "system");
        OnQuery();
    }

    private async void OnDelete()
    {
        if (SelectedPlan == null)
        {
            MessageBox.Show("请选中要删除的方案", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show("确认删除选中的方案？", "确认", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        // 删除时按产品类别路由到 GasDG 仓储（软删除）
        await _testPlanService.DeleteWholeConfigAsync(SelectedPlan.Id, ProductCategory.GasDG, "system");
        OnQuery();
    }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext) { }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        var planType = navigationContext.Parameters.GetValue<string>("PlanType");
        var isTypeChanged = !string.IsNullOrEmpty(planType) && planType != _planTypeKey;

        if (!string.IsNullOrEmpty(planType))
        {
            if (isTypeChanged)
            {
                _planTypeKey = planType;
                ProductCode = string.Empty;
                CustomerCode = string.Empty;
                MaterialNumber = string.Empty;
                SelectedPlan = null;

                if (planType == "WholeSet")
                    InitializeWholeSetParameters();
                else
                    InitializeWholeGetParameters();

                BreadCrumb = planType == "WholeSet" ? "首页 > 整机设置方案配置" : "首页 > 整机检验方案配置";
                OnPropertyChanged(nameof(CurrentPageName));
            }
        }

        // 读取主界面传入的软件物料号，用于面包屑显示
        var materialNumber = navigationContext.Parameters.GetValue<string>("MaterialNumber");
        if (!string.IsNullOrEmpty(materialNumber))
        {
            CurrentMaterialNumber = materialNumber;
        }

        // 仅在首次进入或类型切换时自动查询，避免重复导航导致参数重新加载
        if (isTypeChanged || Plans.Count == 0)
        {
            OnQuery();
        }
    }
}
