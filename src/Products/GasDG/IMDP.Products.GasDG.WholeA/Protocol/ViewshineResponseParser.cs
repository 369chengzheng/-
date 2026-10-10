namespace IMDP.Products.GasDG.WholeA.Protocol;

/// <summary>
/// 威星物联网燃气表生产测试通讯协议 — 响应数据解析器
/// <para>
/// 本类负责将协议帧中数据域的"数据内容"部分（即 <see cref="DataDomainParts.DataContent"/>）
/// 解析为强类型的数据模型对象。对应协议文档第 4 章"数据格式"中定义的各数据结构。
/// </para>
/// <para>
/// 解析流程：
/// <list type="number">
///   <item>使用 <see cref="ViewshineProtocolParser.TryParseFrame"/> 解析原始字节流得到 <see cref="FrameParseResult"/></item>
///   <item>使用 <see cref="ViewshineProtocolParser.ParseDataDomain"/> 拆分数据域得到 <see cref="DataDomainParts"/></item>
///   <item>根据 <see cref="DataDomainParts.DataIdentifier"/> 选择对应的解析方法</item>
///   <item>调用本类方法将 <see cref="DataDomainParts.DataContent"/> 解析为具体模型</item>
/// </list>
/// </para>
/// <example>
/// 示例：完整解析一个读取出厂配置参数的响应帧
/// <code>
/// var frameParser = new ViewshineProtocolParser();
/// var responseParser = new ViewshineResponseParser(frameParser);
///
/// // 从串口缓冲区解析帧
/// var result = frameParser.TryParseFrame(buffer, 0, buffer.Length);
/// if (result.IsComplete &amp;&amp; result.Data != null)
/// {
///     var parts = frameParser.ParseDataDomain(result.Data);
///     if (parts.DataIdentifier == ViewshineProtocolConstants.DataIdFactoryConfig)
///     {
///         FactoryConfigResponse config = responseParser.ParseFactoryConfigResponse(parts.DataContent);
///         Console.WriteLine($"表号: {config.MeterNumber}");
///         Console.WriteLine($"剩余金额: {config.RemainingAmount:F2}");
///     }
/// }
/// </code>
/// </example>
/// </summary>
public class ViewshineResponseParser
{
    private readonly ViewshineProtocolParser _parser;

    /// <summary>
    /// 初始化响应解析器
    /// </summary>
    /// <param name="parser">协议帧解析器实例（提供 BCD/小端序/ASCII 等工具方法）。若为 null 则自动创建。</param>
    public ViewshineResponseParser(ViewshineProtocolParser? parser = null)
    {
        _parser = parser ?? new ViewshineProtocolParser();
    }

    // ==================================================================================
    // 4.1.2 读出厂配置参数响应（数据标识 0xC518，控制码 0x81）
    // ==================================================================================

    /// <summary>
    /// 解析读出厂配置参数响应（4.1.2）
    /// <para>
    /// 数据内容结构（按字节顺序）：
    /// 表具编号(7B BCD) + 表具参数(变长) + 剩余金额(4B) + 出厂配置参数(14B) +
    /// 表具底码(4B) + 阀门配置(6B) + 电源配置(10B) + 计费类型(1B) +
    /// GPRS状态(1B) + 信号强度(1B) + TCP状态(1B) + SIM状态(1B) +
    /// 温度压力(10B) + IMSI(20B) + 启用标志(1B) + 域名长度(1B) + 域名(N) + 域名端口(2B) +
    /// 单价(4B) + 卡类型(1B) + 加密标志(1B) + 表具规格(1B) + 传感器类型(2B) +
    /// 通讯电池电压(1B) + 计量电池电压(1B) + 表具状态字(4B)
    /// </para>
    /// </summary>
    /// <param name="dataContent">数据内容字节数组（来自 <see cref="DataDomainParts.DataContent"/>）</param>
    /// <returns>解析后的 <see cref="FactoryConfigResponse"/> 对象</returns>
    /// <example>
    /// <code>
    /// var resp = responseParser.ParseFactoryConfigResponse(dataContent);
    /// string meterNo = resp.MeterNumber;            // 表号
    /// decimal remaining = resp.RemainingAmount;      // 剩余金额
    /// bool valveOk = resp.StatusWord.ValveState == ValveStatus.Open;
    /// </code>
    /// </example>
    public FactoryConfigResponse ParseFactoryConfigResponse(byte[]? dataContent)
    {
        var response = new FactoryConfigResponse();
        if (dataContent == null || dataContent.Length == 0) return response;

        int offset = 0;

        // 表具编号（7B BCD，13 位，低位在前）
        var meterBcd = new byte[7];
        CopySafe(dataContent, offset, meterBcd, 7);
        response.MeterNumber = ViewshineProtocolParser.BcdToMeterNumber(meterBcd);
        offset += 7;

        // 表具参数（变长，包含网络配置等变长字段）
        int meterParamsConsumed;
        response.MeterParameters = ParseMeterParameters(dataContent, offset, out meterParamsConsumed);
        offset += meterParamsConsumed;

        // 剩余金额（4B HEX，低位在前，2 位小数）
        response.RemainingAmount = ViewshineProtocolParser.ReadLittleEndianDecimal(dataContent, offset, 2);
        offset += 4;

        // 表具出厂配置参数（14B）
        response.FactoryParams = CopySafe(dataContent, offset, 14);
        offset += 14;

        // 表具底码（4B HEX，低位在前，2 位小数）
        response.BaseCode = ViewshineProtocolParser.ReadLittleEndianDecimal(dataContent, offset, 2);
        offset += 4;

        // 阀门配置参数（6B）
        response.ValveConfig = CopySafe(dataContent, offset, 6);
        offset += 6;

        // 电源管理配置参数（10B）
        response.PowerConfig = CopySafe(dataContent, offset, 10);
        offset += 10;

        // 表计费类型（1B：0=金额式, 1=气量）
        response.BillingType = (BillingType)SafeReadByte(dataContent, offset);
        offset += 1;

        // GPRS 模块状态（1B）
        response.GprsModuleStatus = SafeReadByte(dataContent, offset);
        offset += 1;

        // 信号强度值（1B）
        response.SignalStrength = SafeReadByte(dataContent, offset);
        offset += 1;

        // TCP 连接状态（1B）
        response.TcpConnectionStatus = SafeReadByte(dataContent, offset);
        offset += 1;

        // SIM 卡状态（1B）
        response.SimCardStatus = SafeReadByte(dataContent, offset);
        offset += 1;

        // 温度压力（10B: 温度状态1B + 压力状态1B + 温度值4B + 压力值4B）
        response.TemperaturePressure = ParseTemperaturePressureData(dataContent, offset);
        offset += 10;

        // IMSI 号（20B ASCII）
        response.Imsi = ViewshineProtocolParser.ReadAsciiString(dataContent, offset, 20);
        offset += 20;

        // 启用标志（1B：0x20=启用域名，其他=启用IP）
        response.DomainEnableFlag = SafeReadByte(dataContent, offset);
        offset += 1;

        // 域名长度（1B）
        int domainLength = SafeReadByte(dataContent, offset);
        offset += 1;

        // 域名（N ASCII）
        if (domainLength > 0 && offset + domainLength <= dataContent.Length)
        {
            response.Domain = ViewshineProtocolParser.ReadAsciiString(dataContent, offset, domainLength);
            offset += domainLength;
        }

        // 域名端口（2B HEX）
        if (offset + 2 <= dataContent.Length)
        {
            response.DomainPort = dataContent[offset] | (dataContent[offset + 1] << 8);
            offset += 2;
        }

        // 单价（4B HEX，低位在前，4 位小数）
        response.UnitPrice = ViewshineProtocolParser.ReadLittleEndianDecimal(dataContent, offset, 4);
        offset += 4;

        // 卡类型（1B）
        response.CardType = SafeReadByte(dataContent, offset);
        offset += 1;

        // 安全表是否加密（1B：0=否, 1=是）
        response.IsEncrypted = SafeReadByte(dataContent, offset) == 1;
        offset += 1;

        // 表具规格（1B）
        response.MeterSpec = (MeterSpecification)SafeReadByte(dataContent, offset);
        offset += 1;

        // 温压传感器类型（2B: 温度传感器类型1B + 压力传感器类型1B）
        response.SensorTypes = new TempPressureSensorType
        {
            TemperatureSensor = (TemperatureSensorType)SafeReadByte(dataContent, offset),
            PressureSensor = (PressureSensorType)SafeReadByte(dataContent, offset + 1)
        };
        offset += 2;

        // 通讯电池电压（1B HEX，如 0x2D 表示 4.5V，即 值/10）
        byte commVoltage = SafeReadByte(dataContent, offset);
        response.CommBatteryVoltage = commVoltage / 10m;
        offset += 1;

        // 计量电池电压（1B HEX）
        byte measureVoltage = SafeReadByte(dataContent, offset);
        response.MeasureBatteryVoltage = measureVoltage / 10m;
        offset += 1;

        // 表具状态字（4B）
        response.StatusWord = ParseMeterStatusWord(dataContent, offset);

        return response;
    }

    // ==================================================================================
    // 4.1.3 读表具参数（变长结构，含网络配置 2+N+n）
    // ==================================================================================

    /// <summary>
    /// 解析读表具参数数据（4.1.3）
    /// <para>
    /// 该结构为变长（因网络配置参数为 2+N+n 字节），解析完成后通过 <paramref name="consumed"/> 返回实际消耗的字节数，
    /// 供调用方继续解析后续字段。
    /// </para>
    /// <para>
    /// 字段顺序：表具时间(6B) + 通讯类型(1B) + 表具类型(1B) + 运行模式(1B) + 表存上限(4B) +
    /// 预付费参数(10B) + 抄表时间(11B) + 月冻结日(3B) + 网络参数(2+N+n) + IP地址(7B) +
    /// 模块终端号(20B) + 软件版本(6B) + 唤醒周期生效(1B) + 唤醒周期(11B) + 备用电池抄表(11B)
    /// </para>
    /// </summary>
    /// <param name="data">数据内容字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <param name="consumed">输出：本次解析消耗的字节数</param>
    /// <returns>解析后的 <see cref="MeterParameters"/> 对象</returns>
    public MeterParameters ParseMeterParameters(byte[] data, int offset, out int consumed)
    {
        var parameters = new MeterParameters();
        int start = offset;
        consumed = 0;
        if (data == null || offset >= data.Length) return parameters;

        // 表具时间（6B BCD: YYMMDDhhmmss）
        parameters.MeterTime = ParseSystemClock(data, offset);
        offset += 6;

        // 通讯类型（1B：0=移动流量, 1=电信流量）
        parameters.CommType = (CommunicationType)SafeReadByte(data, offset);
        offset += 1;

        // 表具类型（1B：0=霍尔, 1=光电, 2=超声波）
        parameters.MeterType = (MeterType)SafeReadByte(data, offset);
        offset += 1;

        // 运行模式（1B：0=表具后付费, 1=表具预付费, 2=后台预付费）
        parameters.RunMode = (RunMode)SafeReadByte(data, offset);
        offset += 1;

        // 表存上限（4B HEX，低位在前，2 位小数）
        parameters.StorageLimit = ViewshineProtocolParser.ReadLittleEndianDecimal(data, offset, 2);
        offset += 4;

        // 预付费参数（10B）
        parameters.PrepaidParams = CopySafe(data, offset, 10);
        offset += 10;

        // 抄表时间参数（11B）
        parameters.ReadingTime = ParseReadingTimeParams(data, offset);
        offset += 11;

        // 月冻结日参数（3B BCD: 日、时、分）
        parameters.MonthlyFreeze = ParseMonthlyFreezeParams(data, offset);
        offset += 3;

        // 网络配置参数（2+N+n 变长）
        int netConsumed;
        parameters.NetworkConfig = ParseNetworkConfig(data, offset, out netConsumed);
        offset += netConsumed;

        // IP 地址配置参数（7B）
        parameters.IpConfig = ParseIpConfig(data, offset);
        offset += 7;

        // 模块终端号（20B ASCII，不足补空格）
        parameters.TerminalNumber = ViewshineProtocolParser.ReadAsciiString(data, offset, 20);
        offset += 20;

        // 软件版本号（6B）
        parameters.SoftwareVersion = ParseMeterVersion(data, offset);
        offset += 6;

        // 唤醒周期是否生效（1B：0=不生效, 1=生效）
        parameters.WakeCycleEnabled = SafeReadByte(data, offset) == 1;
        offset += 1;

        // 唤醒周期参数（11B）
        parameters.WakeCycle = ParseReadingTimeParams(data, offset);
        offset += 11;

        // 备用电池抄表时间（11B）
        parameters.BackupBatteryReadingTime = ParseReadingTimeParams(data, offset);
        offset += 11;

        consumed = offset - start;
        return parameters;
    }

    // ==================================================================================
    // 4.2.2 读取加密及平台信息响应（数据标识 0xC519）
    // ==================================================================================

    /// <summary>
    /// 解析读取加密及平台信息响应（4.2.2）
    /// <para>
    /// 字段顺序：表具编号(32B) + 加密密钥长度(1B) + 加密密钥(32B) + 认证密钥长度(1B) + 认证密钥(32B) +
    /// Onenet平台参数(15B) + PSM模式(1B) + 反向累积量(4B) + 表型号(2B) +
    /// 生产状态(1B) + 加密芯片状态(1B) + 保留(1B) + IMEI(15B) + 硬件芯片ID(8B) +
    /// 关阀类型(1B) + K值(4B) + B值(4B) + 网络类型(1B)
    /// </para>
    /// </summary>
    /// <param name="dataContent">数据内容字节数组</param>
    /// <returns>解析后的 <see cref="EncryptionPlatformInfoResponse"/> 对象</returns>
    public EncryptionPlatformInfoResponse ParseEncryptionInfoResponse(byte[]? dataContent)
    {
        var response = new EncryptionPlatformInfoResponse();
        if (dataContent == null || dataContent.Length == 0) return response;

        int offset = 0;

        // 表具编号（32B ASCII）
        response.MeterNumber = ViewshineProtocolParser.ReadAsciiString(dataContent, offset, 32);
        offset += 32;

        // 加密密钥长度（1B，默认 24）
        response.EncryptionKeyLength = SafeReadByte(dataContent, offset);
        offset += 1;

        // 加密密钥（32B HEX）
        response.EncryptionKey = CopySafe(dataContent, offset, 32);
        offset += 32;

        // 认证密钥长度（1B，默认 24）
        response.AuthKeyLength = SafeReadByte(dataContent, offset);
        offset += 1;

        // 认证密钥（32B HEX）
        response.AuthKey = CopySafe(dataContent, offset, 32);
        offset += 32;

        // Onenet 平台参数（15B）
        response.OnenetParams = ParseOnenetPlatformParams(dataContent, offset);
        offset += 15;

        // PSM 模式（1B：0=掉电模式, 1=PSM模式）
        response.PsmMode = SafeReadByte(dataContent, offset);
        offset += 1;

        // 反向累积量（4B HEX，低位在前，2 位小数）
        response.ReverseAccumulation = ViewshineProtocolParser.ReadLittleEndianDecimal(dataContent, offset, 2);
        offset += 4;

        // 表型号（2B: 表类型1B + 量程1B）
        response.MeterModel = new MeterModel
        {
            MeterTypeCode = SafeReadByte(dataContent, offset),
            RangeCode = SafeReadByte(dataContent, offset + 1)
        };
        offset += 2;

        // 表具生产状态（1B）
        response.ProductionStatus = (ProductionStatus)SafeReadByte(dataContent, offset);
        offset += 1;

        // 加密芯片状态结果（1B）
        response.ChipStatus = (EncryptionChipStatus)SafeReadByte(dataContent, offset);
        offset += 1;

        // 保留（1B）
        response.Reserved = SafeReadByte(dataContent, offset);
        offset += 1;

        // IMEI 号（15B ASCII）
        response.Imei = ViewshineProtocolParser.ReadAsciiString(dataContent, offset, 15);
        offset += 15;

        // 硬件加密芯片 ID 号（8B HEX）
        response.HardwareChipId = CopySafe(dataContent, offset, 8);
        offset += 8;

        // 参数配置关阀类型（1B: B0B1=00-不关阀, 01-普通关阀, 10-强制关阀）
        response.ValveCloseType = SafeReadByte(dataContent, offset);
        offset += 1;

        // 调齿比系数 K 值（4B HEX，低位在前，6 位小数）
        response.KValue = ViewshineProtocolParser.ReadLittleEndianDecimal(dataContent, offset, 6);
        offset += 4;

        // 调齿比系数 B 值（4B HEX，低位在前，6 位小数）
        response.BValue = ViewshineProtocolParser.ReadLittleEndianDecimal(dataContent, offset, 6);
        offset += 4;

        // 通讯板的网络类型（1B）
        response.NetworkType = (NetworkType)SafeReadByte(dataContent, offset);

        return response;
    }

    // ==================================================================================
    // 4.3.2 读取配置参数响应（数据标识 0xC620）
    // ==================================================================================

    /// <summary>
    /// 解析读取配置参数响应（4.3.2）
    /// <para>
    /// 字段顺序：甲烷低报(2B) + 甲烷高报(2B) + 压力传感器1(8B) + 压力传感器2(8B) +
    /// 温压传感器1配置(1B) + 温压传感器2配置(1B) + 风机配置(1B) + 报警器配置(1B) + 开盖配置(1B)
    /// </para>
    /// </summary>
    /// <param name="dataContent">数据内容字节数组</param>
    /// <returns>解析后的 <see cref="ConfigurationParamsResponse"/> 对象</returns>
    public ConfigurationParamsResponse ParseConfigParamsResponse(byte[]? dataContent)
    {
        var response = new ConfigurationParamsResponse();
        if (dataContent == null || dataContent.Length == 0) return response;

        int offset = 0;

        // 甲烷低报报警参数（2B，百分比 %LEL）
        response.MethaneLowAlarm = ReadLittleEndianUShort(dataContent, offset);
        offset += 2;

        // 甲烷高报报警参数（2B，百分比 %LEL）
        response.MethaneHighAlarm = ReadLittleEndianUShort(dataContent, offset);
        offset += 2;

        // 压力传感器 1 参数（8B）
        response.PressureSensor1 = ParsePressureSensorParams(dataContent, offset);
        offset += 8;

        // 压力传感器 2 参数（8B）
        response.PressureSensor2 = ParsePressureSensorParams(dataContent, offset);
        offset += 8;

        // 温压传感器 1 配置（1B：0=无, 1~3=有）
        response.TempPressureSensor1Config = SafeReadByte(dataContent, offset);
        offset += 1;

        // 温压传感器 2 配置（1B：0=无, 1~3=有）
        response.TempPressureSensor2Config = SafeReadByte(dataContent, offset);
        offset += 1;

        // 风机配置（1B：0=无, 1=有）
        response.HasFan = SafeReadByte(dataContent, offset) == 1;
        offset += 1;

        // 报警器配置（1B：0=无, 1=有）
        response.HasAlarm = SafeReadByte(dataContent, offset) == 1;
        offset += 1;

        // 开盖配置（1B：0=关闭, 1=开启）
        response.OpenCoverEnabled = SafeReadByte(dataContent, offset) == 1;

        return response;
    }

    // ==================================================================================
    // 4.4 读取网络参数及终端号响应（数据标识 0xC525）
    // ==================================================================================

    /// <summary>
    /// 解析读取网络参数及终端号响应（4.4）
    /// <para>
    /// 字段顺序：IMEI(15B) + IMSI(15B) + ICCID(20B) + CSQ(1B) + RSRP(2B) + SNR(2B) +
    /// ECL(1B) + CellID(4B) + ICCID2(20B) + IMSI2(15B) + CSQ2(1B) +
    /// 软件编译时间(6B) + 外部压力状态(1B) + 外部压力值(4B) + 蓝牙SN(12B)
    /// </para>
    /// </summary>
    /// <param name="dataContent">数据内容字节数组</param>
    /// <returns>解析后的 <see cref="NetworkParamsTerminalInfo"/> 对象</returns>
    public NetworkParamsTerminalInfo ParseNetworkParamsResponse(byte[]? dataContent)
    {
        var response = new NetworkParamsTerminalInfo();
        if (dataContent == null || dataContent.Length == 0) return response;

        int offset = 0;

        // IMEI 号（15B ASCII）
        response.Imei = ViewshineProtocolParser.ReadAsciiString(dataContent, offset, 15);
        offset += 15;

        // IMSI 号（15B ASCII）
        response.Imsi = ViewshineProtocolParser.ReadAsciiString(dataContent, offset, 15);
        offset += 15;

        // ICCID 号（20B ASCII）
        response.Iccid = ViewshineProtocolParser.ReadAsciiString(dataContent, offset, 20);
        offset += 20;

        // CSQ 值（1B HEX）
        response.Csq = SafeReadByte(dataContent, offset);
        offset += 1;

        // RSRP 值（2B HEX，低位在前，有符号；0x7EFF 表示 -130）
        response.Rsrp = ReadLittleEndianShort(dataContent, offset);
        offset += 2;

        // SNR 值（2B HEX，低位在前，有符号）
        response.Snr = ReadLittleEndianShort(dataContent, offset);
        offset += 2;

        // ECL 信号等级（1B：0~6）
        response.Ecl = SafeReadByte(dataContent, offset);
        offset += 1;

        // CellID 小区号（4B HEX）
        response.CellId = ReadLittleEndianUInt(dataContent, offset);
        offset += 4;

        // 第二张卡 ICCID 号（20B ASCII）
        response.Iccid2 = ViewshineProtocolParser.ReadAsciiString(dataContent, offset, 20);
        offset += 20;

        // 第二张卡 IMSI 号（15B ASCII）
        response.Imsi2 = ViewshineProtocolParser.ReadAsciiString(dataContent, offset, 15);
        offset += 15;

        // 第二张卡 CSQ 值（1B HEX）
        response.Csq2 = SafeReadByte(dataContent, offset);
        offset += 1;

        // 软件版本编译时间（6B BCD: YYMMDDhhmmss）
        response.SoftwareCompileTime = ParseSystemClock(dataContent, offset);
        offset += 6;

        // 外部压力传感状态（1B：0=正常, 1=异常）
        response.ExternalPressureAbnormal = SafeReadByte(dataContent, offset) == 1;
        offset += 1;

        // 外部压力传感器值（4B HEX，低位在前，2 位小数）
        response.ExternalPressureValue = ViewshineProtocolParser.ReadLittleEndianDecimal(dataContent, offset, 2);
        offset += 4;

        // 蓝牙模块 SN 号（12B ASCII）
        response.BluetoothSn = ViewshineProtocolParser.ReadAsciiString(dataContent, offset, 12);

        return response;
    }

    // ==================================================================================
    // 4.5 读取设备运行信息响应（数据标识 0xC527）
    // ==================================================================================

    /// <summary>
    /// 解析读取设备运行信息响应（4.5）
    /// <para>字段顺序：报警器状态(1B) + 报警器浓度(1B) + RS485状态(1B) + 蓝牙状态(1B)</para>
    /// </summary>
    /// <param name="dataContent">数据内容字节数组</param>
    /// <returns>解析后的 <see cref="DeviceRunningInfo"/> 对象</returns>
    /// <example>
    /// <code>
    /// var info = responseParser.ParseDeviceRunningInfo(dataContent);
    /// if (info.AlarmTriggered)
    ///     Console.WriteLine($"报警器已触发！浓度: {info.AlarmConcentration}%LEL");
    /// </code>
    /// </example>
    public DeviceRunningInfo ParseDeviceRunningInfo(byte[]? dataContent)
    {
        var info = new DeviceRunningInfo();
        if (dataContent == null || dataContent.Length == 0) return info;

        // 报警器状态（1B：0=已触发, 1=未触发）
        info.AlarmTriggered = SafeReadByte(dataContent, 0) == 0;

        // 报警器浓度（1B：百分比 0%~100%，单位 %LEL）
        info.AlarmConcentration = SafeReadByte(dataContent, 1);

        // RS485 接口状态（1B：0=异常, 1=正常）
        info.Rs485Normal = SafeReadByte(dataContent, 2) == 1;

        // 蓝牙连接状态（1B：0=未连接, 1=已连接）
        info.BluetoothConnected = SafeReadByte(dataContent, 3) == 1;

        return info;
    }

    // ==================================================================================
    // 4.6 蓝牙控制响应（数据标识 0xA027，控制码 0x84）
    // ==================================================================================

    /// <summary>
    /// 解析蓝牙控制响应（4.6 上行）
    /// <para>字段顺序：结果(1B) + 保留(4B)</para>
    /// </summary>
    /// <param name="dataContent">数据内容字节数组</param>
    /// <returns>解析后的 <see cref="BluetoothControlResult"/> 对象</returns>
    public BluetoothControlResult ParseBluetoothControlResult(byte[]? dataContent)
    {
        var result = new BluetoothControlResult();
        if (dataContent == null || dataContent.Length == 0) return result;

        // 操作结果（1B：0=成功, 1=不支持）
        result.Success = SafeReadByte(dataContent, 0) == 0;

        // 保留（4B）
        result.Reserved = CopySafe(dataContent, 1, 4);

        return result;
    }

    // ==================================================================================
    // 子结构解析方法
    // ==================================================================================

    /// <summary>
    /// 解析系统时钟（6B BCD: 年月日时分秒）
    /// <para>当 Year=0xFF 时表示时钟无效。</para>
    /// </summary>
    /// <param name="data">数据字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <returns>解析后的 <see cref="SystemClock"/> 对象</returns>
    public SystemClock ParseSystemClock(byte[] data, int offset)
    {
        if (data == null || offset + 6 > data.Length)
            return new SystemClock();

        return new SystemClock
        {
            Year = data[offset],
            Month = data[offset + 1],
            Day = data[offset + 2],
            Hour = data[offset + 3],
            Minute = data[offset + 4],
            Second = data[offset + 5]
        };
    }

    /// <summary>
    /// 解析抄表时间参数（11B）
    /// <para>
    /// 结构：抄表类型(1B) + 5 组抄表时间槽（每组 2B: 周期1B + 时间/日期2B BCD）
    /// </para>
    /// <para>多余字节填 0xFF。</para>
    /// </summary>
    /// <param name="data">数据字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <returns>解析后的 <see cref="ReadingTimeParams"/> 对象</returns>
    public ReadingTimeParams ParseReadingTimeParams(byte[] data, int offset)
    {
        var result = new ReadingTimeParams();
        if (data == null || offset + 11 > data.Length) return result;

        // 抄表类型（1B）
        result.Type = (ReadingType)data[offset];
        int pos = offset + 1;

        // 5 组抄表时间槽（每组 2B：周期1B + 时间2B + 日期2B）
        // 注意：协议定义 10 字节为 5 组，每组 2B（时间/日期二选一）
        // 实际按文档示例：周期1B + 时间2B(BCD) + 日期2B(BCD)，但 10 字节容纳不下 5*5
        // 重新理解：10 字节 = 5 组 × 2 字节，每组为时间或日期
        for (int i = 0; i < 5 && pos + 2 <= data.Length; i++)
        {
            var slot = new ReadingTimeSlot
            {
                Period = data[pos],
                TimeValue = new byte[] { data[pos + 1], pos + 2 < data.Length ? data[pos + 2] : (byte)0xFF }
            };
            // 日期值在按日模式下使用，此处仅占位
            slot.DateValue = new byte[] { 0xFF, 0xFF };
            result.Slots.Add(slot);
            pos += 2;
        }

        return result;
    }

    /// <summary>
    /// 解析网络配置参数（2+N+n 变长）
    /// <para>结构：用户名长度(1B) + 用户名(N ASCII) + 密码长度(1B) + 密码(n ASCII)</para>
    /// </summary>
    /// <param name="data">数据字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <param name="consumed">输出：本次解析消耗的字节数</param>
    /// <returns>解析后的 <see cref="NetworkConfig"/> 对象</returns>
    public NetworkConfig ParseNetworkConfig(byte[] data, int offset, out int consumed)
    {
        var config = new NetworkConfig();
        consumed = 0;
        if (data == null || offset >= data.Length) return config;

        int start = offset;

        // 用户名长度（1B）
        int userNameLen = SafeReadByte(data, offset);
        offset += 1;

        // 用户名（N ASCII）
        if (userNameLen > 0 && offset + userNameLen <= data.Length)
        {
            config.UserName = ViewshineProtocolParser.ReadAsciiString(data, offset, userNameLen);
            offset += userNameLen;
        }

        // 密码长度（1B）
        int passwordLen = SafeReadByte(data, offset);
        offset += 1;

        // 密码（n ASCII）
        if (passwordLen > 0 && offset + passwordLen <= data.Length)
        {
            config.Password = ViewshineProtocolParser.ReadAsciiString(data, offset, passwordLen);
            offset += passwordLen;
        }

        consumed = offset - start;
        return config;
    }

    /// <summary>
    /// 解析 IP 地址配置参数（7B）
    /// <para>结构：启用标志(1B) + IP地址(4B) + 端口(2B)</para>
    /// </summary>
    /// <param name="data">数据字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <returns>解析后的 <see cref="IpConfig"/> 对象</returns>
    /// <example>
    /// <code>
    /// var ipConfig = responseParser.ParseIpConfig(data, offset);
    /// Console.WriteLine($"IP: {ipConfig.IpAddress}:{ipConfig.Port}");
    /// </code>
    /// </example>
    public IpConfig ParseIpConfig(byte[] data, int offset)
    {
        var config = new IpConfig();
        if (data == null || offset + 7 > data.Length) return config;

        // 启用标志（1B：0x20=启用域名，其他=启用IP）
        config.EnableFlag = data[offset];

        // 目标 IP 地址（4B HEX）
        config.IpAddress = $"{data[offset + 1]}.{data[offset + 2]}.{data[offset + 3]}.{data[offset + 4]}";

        // 端口号（2B HEX）
        config.Port = data[offset + 5] | (data[offset + 6] << 8);

        return config;
    }

    /// <summary>
    /// 解析月冻结日参数（3B BCD: 日、时、分）
    /// <para>日=0x00 表示天冻结，日=1~28 表示月冻结。</para>
    /// </summary>
    /// <param name="data">数据字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <returns>解析后的 <see cref="MonthlyFreezeParams"/> 对象</returns>
    public MonthlyFreezeParams ParseMonthlyFreezeParams(byte[] data, int offset)
    {
        if (data == null || offset + 3 > data.Length)
            return new MonthlyFreezeParams();

        return new MonthlyFreezeParams
        {
            Day = data[offset],
            Hour = data[offset + 1],
            Minute = data[offset + 2]
        };
    }

    /// <summary>
    /// 解析表具版本（6B）
    /// <para>结构：协议版本(1B BCD) + 软件版本(2B BCD) + 硬件版本(2B BCD) + 保留(1B)</para>
    /// </summary>
    /// <param name="data">数据字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <returns>解析后的 <see cref="MeterVersion"/> 对象</returns>
    public MeterVersion ParseMeterVersion(byte[] data, int offset)
    {
        if (data == null || offset + 6 > data.Length)
            return new MeterVersion();

        return new MeterVersion
        {
            ProtocolVersion = data[offset],
            SoftwareVersion = new byte[] { data[offset + 1], data[offset + 2] },
            HardwareVersion = new byte[] { data[offset + 3], data[offset + 4] },
            Reserved = data[offset + 5]
        };
    }

    /// <summary>
    /// 解析表具状态字（4B，32 位）
    /// <para>
    /// 位定义：
    /// <list type="bullet">
    ///   <item>B0~B2: 阀门状态（000=无, 001=开阀, 011=关阀, 101=异常, 111=阀门权关）</item>
    ///   <item>B3: 计量异常</item>
    ///   <item>B4: 小流异常</item>
    ///   <item>B5: 恒流异常</item>
    ///   <item>B6: 大流异常</item>
    ///   <item>B7: 拆表异常</item>
    ///   <item>B8: 温度传感器异常</item>
    ///   <item>B9: 压力传感器异常</item>
    ///   <item>B10: 压力超压</item>
    ///   <item>B11: 压力失压</item>
    ///   <item>B12~B13: 电压状态（00=正常, 01=欠压, 10=低压, 11=掉电）</item>
    ///   <item>B14: 反向异常</item>
    ///   <item>B15: 闲置异常</item>
    ///   <item>B16: 失联异常</item>
    ///   <item>B17: 日用气量上限异常</item>
    ///   <item>B18: 燃气泄漏</item>
    ///   <item>B19: 开盖异常</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="data">数据字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <returns>解析后的 <see cref="MeterStatusWord"/> 对象</returns>
    /// <example>
    /// <code>
    /// var status = responseParser.ParseMeterStatusWord(data, offset);
    /// if (status.HighFlowAlarm) Console.WriteLine("大流异常！");
    /// if (status.VoltageState == VoltageStatus.LowVoltage) Console.WriteLine("低压报警！");
    /// </code>
    /// </example>
    public MeterStatusWord ParseMeterStatusWord(byte[] data, int offset)
    {
        var status = new MeterStatusWord();
        if (data == null || offset + 4 > data.Length) return status;

        // 保存原始数据（4 字节，低位在前）
        status.RawData = new byte[] { data[offset], data[offset + 1], data[offset + 2], data[offset + 3] };

        // 第 0 字节 (B7~B0)
        byte b0 = data[offset];
        status.ValveState = (ValveStatus)(b0 & 0x07);          // B2~B0
        status.MeasurementAlarm = ((b0 >> 3) & 0x01) == 1;      // B3
        status.LowFlowAlarm = ((b0 >> 4) & 0x01) == 1;          // B4
        status.ConstantFlowAlarm = ((b0 >> 5) & 0x01) == 1;     // B5
        status.HighFlowAlarm = ((b0 >> 6) & 0x01) == 1;         // B6
        status.TamperAlarm = ((b0 >> 7) & 0x01) == 1;           // B7

        // 第 1 字节 (B15~B8)
        byte b1 = data[offset + 1];
        status.TemperatureSensorAlarm = (b1 & 0x01) == 1;       // B8
        status.PressureSensorAlarm = ((b1 >> 1) & 0x01) == 1;   // B9
        status.PressureOverAlarm = ((b1 >> 2) & 0x01) == 1;     // B10
        status.PressureLossAlarm = ((b1 >> 3) & 0x01) == 1;     // B11
        status.VoltageState = (VoltageStatus)((b1 >> 4) & 0x03); // B13~B12
        status.ReverseAlarm = ((b1 >> 6) & 0x01) == 1;          // B14
        status.IdleAlarm = ((b1 >> 7) & 0x01) == 1;             // B15

        // 第 2 字节 (B23~B16)
        byte b2 = data[offset + 2];
        status.DisconnectAlarm = (b2 & 0x01) == 1;              // B16
        status.DailyUsageLimitAlarm = ((b2 >> 1) & 0x01) == 1;  // B17
        status.GasLeakAlarm = ((b2 >> 2) & 0x01) == 1;          // B18
        status.OpenCoverAlarm = ((b2 >> 3) & 0x01) == 1;        // B19

        // 第 3 字节 (B31~B24) 全部预留，不解析

        return status;
    }

    /// <summary>
    /// 解析温度压力数据（10B）
    /// <para>结构：温度状态(1B) + 压力状态(1B) + 温度值(4B) + 压力值(4B)</para>
    /// </summary>
    /// <param name="data">数据字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <returns>解析后的 <see cref="TemperaturePressureData"/> 对象</returns>
    public TemperaturePressureData ParseTemperaturePressureData(byte[] data, int offset)
    {
        var result = new TemperaturePressureData();
        if (data == null || offset + 10 > data.Length) return result;

        // 温度状态标志（1B：0=正常, 1=异常）
        result.TemperatureAbnormal = data[offset] == 1;

        // 压力状态标志（1B：0=正常, 1=异常）
        result.PressureAbnormal = data[offset + 1] == 1;

        // 温度值（4B HEX，低位在前，2 位小数）
        result.Temperature = ViewshineProtocolParser.ReadLittleEndianDecimal(data, offset + 2, 2);

        // 压力值（4B HEX，低位在前，2 位小数）
        result.Pressure = ViewshineProtocolParser.ReadLittleEndianDecimal(data, offset + 6, 2);

        return result;
    }

    /// <summary>
    /// 解析 Onenet 平台参数（15B，4.2.3）
    /// <para>
    /// 结构：引用ID(2B) + 对象标识(2B) + 实例标识(2B) + 资源标识上行(2B) +
    /// 资源标识下行(2B) + 数据格式类型(1B) + 结束标志(1B) + 回应ID(2B) + 平台类型(1B)
    /// </para>
    /// </summary>
    /// <param name="data">数据字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <returns>解析后的 <see cref="OnenetPlatformParams"/> 对象</returns>
    public OnenetPlatformParams ParseOnenetPlatformParams(byte[] data, int offset)
    {
        var result = new OnenetPlatformParams();
        if (data == null || offset + 15 > data.Length) return result;

        // ONENET 通信实例的引用 ID（2B，默认 0）
        result.ReferenceId = ReadLittleEndianUShort(data, offset);
        offset += 2;

        // 对象标识（2B，默认 3202）
        result.ObjectId = ReadLittleEndianUShort(data, offset);
        offset += 2;

        // 实例标识（2B，默认 0）
        result.InstanceId = ReadLittleEndianUShort(data, offset);
        offset += 2;

        // 资源标识上行（2B，默认 5500）
        result.ResourceIdUp = ReadLittleEndianUShort(data, offset);
        offset += 2;

        // 资源标识下行（2B，默认 1001）
        result.ResourceIdDown = ReadLittleEndianUShort(data, offset);
        offset += 2;

        // 数据格式类型（1B，默认 1）
        result.DataValueType = data[offset];
        offset += 1;

        // 数据帧结束标志（1B，默认 1）
        result.Flag = data[offset];
        offset += 1;

        // 回应 ID（2B，默认 0）
        result.AckId = ReadLittleEndianUShort(data, offset);
        offset += 2;

        // Onenet 平台类型（1B：0=各省平台, 1=全国平台）
        result.PlatformType = data[offset];

        return result;
    }

    /// <summary>
    /// 解析压力传感器参数（8B，4.3.3）
    /// <para>结构：型号(1B) + 类型(1B) + 量程(2B) + 保留(4B)</para>
    /// </summary>
    /// <param name="data">数据字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <returns>解析后的 <see cref="PressureSensorParams"/> 对象</returns>
    public PressureSensorParams ParsePressureSensorParams(byte[] data, int offset)
    {
        var result = new PressureSensorParams();
        if (data == null || offset + 8 > data.Length) return result;

        // 压力传感器型号（1B：0=无, 1=MPM3801, 2=HP5834）
        result.Model = data[offset];

        // 压力传感器类型（1B：0=无, 1=差压, 2=绝压）
        result.Type = data[offset + 1];

        // 压力传感器量程（2B，单位 hPa/百帕）
        result.Range = ReadLittleEndianUShort(data, offset + 2);

        // 保留（4B）
        result.Reserved = new byte[] { data[offset + 4], data[offset + 5], data[offset + 6], data[offset + 7] };

        return result;
    }

    // ==================================================================================
    // 私有工具方法
    // ==================================================================================

    /// <summary>
    /// 安全读取单字节（越界返回 0）
    /// </summary>
    private static byte SafeReadByte(byte[] data, int offset)
    {
        if (data == null || offset < 0 || offset >= data.Length) return 0;
        return data[offset];
    }

    /// <summary>
    /// 安全拷贝字节数组（越界自动截断）
    /// </summary>
    private static byte[] CopySafe(byte[] data, int offset, int length)
    {
        if (data == null || offset < 0 || offset >= data.Length) return Array.Empty<byte>();
        int actualLen = Math.Min(length, data.Length - offset);
        var result = new byte[actualLen];
        Array.Copy(data, offset, result, 0, actualLen);
        return result;
    }

    /// <summary>
    /// 安全拷贝到目标数组
    /// </summary>
    private static void CopySafe(byte[] source, int sourceOffset, byte[] destination, int length)
    {
        if (source == null || destination == null) return;
        int actualLen = Math.Min(length, Math.Min(source.Length - sourceOffset, destination.Length));
        if (actualLen <= 0) return;
        Array.Copy(source, sourceOffset, destination, 0, actualLen);
    }

    /// <summary>
    /// 读取小端序无符号 16 位整数
    /// </summary>
    private static ushort ReadLittleEndianUShort(byte[] data, int offset)
    {
        if (data == null || offset + 2 > data.Length) return 0;
        return (ushort)(data[offset] | (data[offset + 1] << 8));
    }

    /// <summary>
    /// 读取小端序有符号 16 位整数（用于 RSRP/SNR 等负值字段）
    /// </summary>
    private static short ReadLittleEndianShort(byte[] data, int offset)
    {
        if (data == null || offset + 2 > data.Length) return 0;
        return (short)(data[offset] | (data[offset + 1] << 8));
    }

    /// <summary>
    /// 读取小端序无符号 32 位整数
    /// </summary>
    private static uint ReadLittleEndianUInt(byte[] data, int offset)
    {
        if (data == null || offset + 4 > data.Length) return 0;
        return (uint)(data[offset] | (data[offset + 1] << 8)
            | (data[offset + 2] << 16) | (data[offset + 3] << 24));
    }
}
