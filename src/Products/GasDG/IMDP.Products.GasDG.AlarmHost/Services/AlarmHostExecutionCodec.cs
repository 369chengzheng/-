using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using IMDP.Application.DTOs;
using IMDP.Infrastructure.Protocol.GAS_Protocol.Gas_C5F0;
using IMDP.Products.GasDG.AlarmHost.Models;
using static IMDP.Products.GasDG.AlarmHost.Models.AlarmHostFunctionCatalog;

namespace IMDP.Products.GasDG.AlarmHost.Services;

/// <summary>
/// 报警项目与协议值之间的转换；对象编号和读写权限只在功能目录登记
/// 不参与串口、页面状态或方案保存，不修改方案快照
/// </summary>
public static class AlarmHostExecutionCodec
{
    // 依据V2对象09/11：字段宽度、十进制小数位、有符号性只维护一份。
    private static readonly IReadOnlyDictionary<string, (int Length, int Decimals, bool Signed)> NumericFields =
        new Dictionary<string, (int, int, bool)>(StringComparer.Ordinal)
        {
            [ReservedAmount] = (4, 2, false), [LowBalanceThreshold] = (4, 2, false),
            [HighFlowThreshold] = (2, 2, false), [LowFlowThreshold] = (2, 0, false),
            [NoCommunicationDays] = (1, 0, false), [NoGasDays] = (1, 0, false), [SecondNoGasDays] = (1, 0, false),
            [UpperTemperature] = (2, 2, false), [LowerTemperature] = (2, 2, true),
            [UpperPressure] = (2, 1, false), [LowerPressure] = (2, 1, false)
        };

    // 更改掩码位置与阀控值位置不同，按原图25～27逐事件登记，避免用行下标推算。
    private static readonly IReadOnlyDictionary<string, (int MaskByte, int MaskBit, int ValueByte, int Shift)> ValveFields =
        new Dictionary<string, (int, int, int, int)>(StringComparer.Ordinal)
        {
            [TamperValve] = (0, 7, 1, 6), [ConstantFlowValve] = (0, 6, 1, 4),
            [LowFlowValve] = (0, 5, 1, 2), [HighFlowValve] = (0, 4, 1, 0),
            [NoGasValve] = (0, 1, 0, 2), [NoCommunicationValve] = (0, 0, 0, 0),
            [Alarm119Valve] = (1, 1, 2, 2), [CoverValve] = (1, 0, 2, 0),
            [LowBalanceValve] = (2, 7, 5, 4), [LowerPressureValve] = (2, 6, 5, 2),
            [LowerTemperatureValve] = (2, 5, 5, 0), [UpperPressureValve] = (2, 2, 4, 4),
            [UpperTemperatureValve] = (2, 1, 4, 2), [BatteryCoverValve] = (4, 2, 8, 2)
        };

    /// <summary>
    /// 合并本轮启用且勾选“更改”的阀控；未选事件的更改位始终为零。
    /// </summary>
    public static byte[] EncodeValveConfiguration(IReadOnlyList<AlarmHostResultRow> items)
    {
        var value = new byte[18];
        foreach (var item in items.Where(item => bool.Parse(item.Parameters["Change"])))
        {
            var field = ValveFields[item.Code];
            var target = byte.Parse(item.Parameters[SubParameterCodes.Value], CultureInfo.InvariantCulture);
            if (target > 3) throw new FormatException("阀控方式应为0至3。");
            value[field.MaskByte] |= (byte)(1 << field.MaskBit);
            value[8 + field.ValueByte] |= (byte)(target << field.Shift);
        }
        return value;
    }

    /// <summary>
    /// 用于分批预算的最大回读宽度；数值项复用已有字段定义
    /// </summary>
    public static int MaximumReadLength(string code)
    {
        if (NumericFields.TryGetValue(code, out var field)) return field.Length;
        if (ValveFields.ContainsKey(code)) return 18;
        return code switch
        {
            MeterNumber or MeteringVersion or BaseMeterNumber => 33,
            MainServer => 36,
            MainNetwork => 115,
            MeterTime or MeterBottom or ConstantFlowThreshold => 6,
            Lifetime => 4,
            MainPowerReport => 11,
            Imei or Imsi or NetworkOperator => 15,
            Iccid => 20,
            BluetoothSnOrSparkMac or AlarmHostFunctionCatalog.Version => 12,
            ValveStatus or ModemStatus => 8,
            MonthlyFreeze => 3,
            MeterSpecification or ExternalPressure or CommunicationSelfCheck => 2,
            ProductionStatus or HasValve or AlarmThreshold or AlarmRecoveryThreshold or Csq or
                SettlementMode or SettlementFunctions or Compensation or MeteringVerification or
                PulseResolution or MainVoltage or MeteringVoltage => 1,
            _ => throw new InvalidOperationException($"未登记的对象边界：{code}。")
        };
    }

    /// <summary>
    /// 批量回包的字段边界；长度前缀只用于切分，业务值仍由原解码器逐项校验
    /// </summary>
    public static int ReadValueLength(string code, ReadOnlyMemory<byte> remaining)
    {
        // 完整阀控为8字节更改位+10字节值；旧设备仅返回10字节值时必须位于批末尾。
        if (ValveFields.ContainsKey(code) && remaining.Length == 10) return 10;
        if (code is not (MeterNumber or MeteringVersion or BaseMeterNumber or MainServer or MainNetwork))
            return MaximumReadLength(code);
        var offset = code == MainServer ? 1 : 0;
        var fields = code == MainNetwork ? 3 : 1;
        for (var index = 0; index < fields; index++)
        {
            if (offset >= remaining.Length) throw new FormatException("批量对象缺少文本长度字段。");
            var length = remaining.Span[offset];
            if (offset + 1 + length > remaining.Length)
                throw new FormatException("批量对象的文本长度无效。");
            offset += 1 + length;
        }
        return offset + (code == MainServer ? 2 : 0);
    }

    /// <summary>
    /// 根据方案原始参数及本次扫码、时钟生成可写对象的目标值
    /// 表号使用扫码值，表时使用本次设备操作时间，均不回写配置
    /// </summary>
    public static byte[] Encode(AlarmHostResultRow item, string scanCode, DateTime now)
    {
        string P(string code) => item.Parameters[code].Trim();
        byte B(string code = SubParameterCodes.Value) => byte.Parse(P(code), CultureInfo.InvariantCulture);
        decimal Number(string code = SubParameterCodes.Value) => decimal.Parse(P(code), CultureInfo.InvariantCulture);
        if (NumericFields.TryGetValue(item.Code, out var field))
            return field.Signed ? GasDataObjectValues.EncodeSigned16(Number(), field.Decimals) :
                GasDataObjectValues.EncodeUnsigned(Number(), field.Length, field.Decimals);
        if (ValveFields.ContainsKey(item.Code)) return [B()];
        switch (item.Code)
        {
            case MeterNumber: return Prefix(scanCode, 32, false);
            case MeterTime: return EncodeTime(now);
            case ProductionStatus: return [B(SubParameterCodes.Value)];
            case HasValve: return [bool.Parse(P(SubParameterCodes.Value)) ? (byte)1 : (byte)0];
            case Lifetime:
                var life = new byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(life, uint.Parse(P(SubParameterCodes.Value), CultureInfo.InvariantCulture));
                return life;
            case AlarmThreshold:
            case AlarmRecoveryThreshold: return [B(SubParameterCodes.Value)];
            case MainServer:
                var server = Prefix(P("Address"), 32, false);
                var port = new byte[2];
                BinaryPrimitives.WriteUInt16LittleEndian(port, ushort.Parse(P("Port"), CultureInfo.InvariantCulture));
                return [B("AddressType"), .. server, .. port];
            case MainNetwork:
                // 网络内容保留用户原值，长度按实际ASCII字节计算。
                return [.. Prefix(item.Parameters["UserName"], 32), .. Prefix(item.Parameters["Password"], 32), .. Prefix(item.Parameters["Apn"], 48)];
            case MainPowerReport:
                var report = Enumerable.Repeat((byte)0xFF, 11).ToArray();
                report[0] = B("Type");
                if (report[0] is 7 or 8)
                {
                    for (var index = 0; index < 5; index++)
                    {
                        var slot = P($"Slot{index + 1}");
                        report[index * 2 + 1] = Bcd(int.Parse(slot[..2], CultureInfo.InvariantCulture));
                        report[index * 2 + 2] = Bcd(int.Parse(slot[2..], CultureInfo.InvariantCulture));
                    }
                }
                else
                {
                    report[1] = B("Cycle");
                    report[2] = Bcd(B("Day")); report[3] = Bcd(B("Hour")); report[4] = Bcd(B("Minute"));
                }
                return report;
            case MeterBottom:
                var number = Number();
                var integer = decimal.Truncate(number);
                return [.. GasDataObjectValues.EncodeUnsigned(integer, 4), .. GasDataObjectValues.EncodeUnsigned(number - integer, 2, 4)];
            case SettlementMode:
            case Compensation:
            case MeteringVerification:
            case PulseResolution: return [B()];
            case SettlementFunctions:
                return [(byte)((bool.Parse(item.Parameters["Purge"]) ? 1 : 0) |
                    (bool.Parse(item.Parameters["ShowSettlement"]) ? 2 : 0) |
                    (bool.Parse(item.Parameters["VirtualBilling"]) ? 4 : 0))];
            case MonthlyFreeze: return [Bcd(B("Day")), Bcd(B("Hour")), Bcd(B("Minute"))];
            case MeterSpecification: return [B("Type"), B("Specification")];
            case ConstantFlowThreshold:
                return [.. GasDataObjectValues.EncodeUnsigned(Number("Lower"), 2, 3),
                    .. GasDataObjectValues.EncodeUnsigned(Number("Upper"), 2, 3), .. GasDataObjectValues.EncodeUnsigned(Number("Duration"), 2)];
            case CommunicationSelfCheck: return [1, B()];
            default: throw new InvalidOperationException($"{item.Name}没有写入指令。");
        }
    }

    /// <summary>
    /// 解码实际回读并按启用项目核对目标；只读标识项目不强加方案类型限制
    /// 表时沿用旧报警检验的三分钟差值，其余可写参数逐值比较
    /// </summary>
    public static AlarmHostItemResult Evaluate(AlarmHostResultRow item, byte[] value, string scanCode, DateTime now, byte[]? expected = null)
    {
        var (display, mes) = Decode(item.Code, value);
        bool? pass = true;
        var message = "读取成功";
        if (item.Code == MeterTime)
        {
            pass = Math.Abs((now - ReadTime(value)).TotalMinutes) <= 3;
            message = pass == true ? "时间核对通过" : $"目标时间：{now:yyyy-MM-dd HH:mm:ss}, 实际时间：{display}, 相差超过3分钟";
        }
        else if (Get(item.Code).CanWrite && Get(item.Code).ObjectId != 0x0200 && item.Code != CommunicationSelfCheck)
        {
            expected ??= Encode(item, scanCode, now);
            // 上报尾部填充不参与核对；按时上报只核对类型及周期，沿用既有设备规则。
            var length = item.Code == MainPowerReport ? expected[0] switch { 5 => 2, 7 or 8 => 11, _ => 5 } : expected.Length;
            pass = value.Length == expected.Length && value.AsSpan(0, length).SequenceEqual(expected.AsSpan(0, length));
            message = pass == true ? "回读核对通过" : $"目标值：{Decode(item.Code, expected).Display}, 实际值：{display}";
        }
        var result = new AlarmHostItemResult(item.Code, display, mes, pass, message);
        string P(string key = SubParameterCodes.Value) => item.Parameters[key].Trim();
        AlarmHostItemResult Compare(bool matches, string target) => result with
        {
            IsPass = matches, Message = matches ? "目标核对通过" : $"目标值：{target}, 实际值：{result.Value}"
        };
        if (item.Code is BaseMeterNumber or ExternalPressure)
            return result with { IsPass = null, Message = "读取完成，仅展示，未作合格判定" };
        if (ValveFields.ContainsKey(item.Code))
            return Compare(result.MesValue == P(), ChoiceText(item.Code, byte.Parse(P(), CultureInfo.InvariantCulture)));
        if (item.Code is Csq or MainVoltage or MeteringVoltage)
        {
            var lower = P("Lower"); var upper = P("Upper");
            if (lower.Length == 0 && upper.Length == 0) return result;
            var actual = decimal.Parse(result.MesValue, CultureInfo.InvariantCulture);
            var matches = (lower.Length == 0 || actual >= decimal.Parse(lower, CultureInfo.InvariantCulture)) &&
                (upper.Length == 0 || actual <= decimal.Parse(upper, CultureInfo.InvariantCulture)) && (item.Code != Csq || actual != 99);
            return Compare(matches, $"{lower}至{upper}");
        }
        if (item.Code == AlarmHostFunctionCatalog.Version)
        {
            var values = result.MesValue.Split(',');
            var keys = new[] { "Software", "Hardware", "CompileTime" };
            if (keys.All(key => P(key).Length == 0)) return result;
            var matches = keys.Select((key, index) => (Target: P(key), Actual: values[index], Index: index))
                .All(pair => pair.Target.Length == 0 || (pair.Index < 2 ?
                    pair.Target.TrimStart('V', 'v') == pair.Actual :
                    pair.Target == pair.Actual || (pair.Target.Length == 10 && pair.Target == pair.Actual[..10])));
            return Compare(matches, string.Join(",", keys.Select(P)));
        }
        if (item.Code == MeteringVersion) return P().Length == 0 ? result : Compare(result.Value == P(), P());
        if (item.Code is NetworkOperator or ValveStatus) return Compare(result.MesValue == P(), ChoiceText(item.Code, byte.Parse(P(), CultureInfo.InvariantCulture)));
        if (item.Code == ModemStatus)
        {
            // 沿指定UGNB工序：3～6表示模组初始化已完成，不将此判定宣称为上报成功。
            var modem = value[0] & 15;
            return result with { IsPass = modem is >= 3 and <= 6, Message = modem is >= 3 and <= 6 ? "模组初始化完成，具体联网状态见读值" : "模组初始化未通过" };
        }
        if (item.Code == CommunicationSelfCheck)
            return Compare(value[0] == byte.Parse(P(), CultureInfo.InvariantCulture) && value[1] == 0, $"自检类型0x{byte.Parse(P(), CultureInfo.InvariantCulture):X2},自检正常");
        if (item.Code == SettlementFunctions)
            return Compare((value[0] & 7) == Encode(item, scanCode, now)[0], Decode(item.Code, Encode(item, scanCode, now)).Display);
        return result;
    }

    /// <summary>
    /// 严格按对象长度解码；输出实际值，复合值统一采用英文逗号
    /// </summary>
    private static (string Display, string Mes) Decode(string code, byte[] value)
    {
        string N(decimal number) => number.ToString("0.####", CultureInfo.InvariantCulture);
        if (NumericFields.TryGetValue(code, out var field))
        {
            Length(value, field.Length);
            var text = N(field.Signed ? GasDataObjectValues.DecodeSigned16(value, field.Decimals) : GasDataObjectValues.DecodeUnsigned(value, field.Decimals));
            return (text, text);
        }
        if (ValveFields.TryGetValue(code, out var valve))
        {
            // 单对象应答边界已明确，分别接受已观察的10字节值和8+10字节布局。
            if (value.Length is not (10 or 18)) throw new FormatException("事件阀控读取内容应为10或18字节。");
            var state = (byte)((value[value.Length - 10 + valve.ValueByte] >> valve.Shift) & 3);
            return (ChoiceText(code, state), state.ToString(CultureInfo.InvariantCulture));
        }
        switch (code)
        {
            case MeterNumber:
                var index = 0;
                var number = ReadText(value, ref index, 32);
                End(value, index);
                return (number, number);
            case MeterTime:
                var time = ReadTime(value).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                return (time, time);
            case ProductionStatus:
            case HasValve:
                Length(value, 1);
                if (value[0] > 1) throw new FormatException("设备返回无效的状态值。");
                var state = code == HasValve ? value[0] == 1 ? "有阀门" : "无阀门" : ProductionStatusOptions.Single(option => option.Key == value[0]).Value;
                return (state, value[0].ToString(CultureInfo.InvariantCulture));
            case Lifetime:
                Length(value, 4);
                var life = BinaryPrimitives.ReadUInt32LittleEndian(value).ToString(CultureInfo.InvariantCulture);
                return (life, life);
            case AlarmThreshold:
            case AlarmRecoveryThreshold:
            case Csq:
                Length(value, 1);
                if (code == Csq && value[0] > 31 && value[0] != 99) throw new FormatException("CSQ只允许0至31或99。");
                var scalar = value[0].ToString(CultureInfo.InvariantCulture);
                return (scalar, scalar);
            case Imei: return FixedText(value, 15);
            case Imsi: return FixedText(value, 15);
            case Iccid: return FixedText(value, 20);
            case BluetoothSnOrSparkMac: return FixedText(value, 12);
            case AlarmHostFunctionCatalog.Version:
                Length(value, 12);
                var software = string.Join(".", value[..3].Select(Decimal));
                var hardware = string.Join(".", value[3..6].Select(Decimal));
                var compile = ReadTime(value[6..]).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                return ($"V{software},V{hardware},{compile}", $"{software},{hardware},{compile}");
            case MainServer:
                if (value.Length < 4 || value[0] > 1) throw new FormatException("服务器地址类型或长度无效。");
                var offset = 1;
                var address = ReadText(value, ref offset, 32);
                if (value.Length != offset + 2) throw new FormatException("服务器端口长度无效。");
                var port = BinaryPrimitives.ReadUInt16LittleEndian(value.AsSpan(offset));
                return ($"{AddressTypeOptions.Single(option => option.Key == value[0]).Value},{address},{port}", $"{value[0]},{address},{port}");
            case MainNetwork:
                var cursor = 0;
                var user = ReadText(value, ref cursor, 32);
                var password = ReadText(value, ref cursor, 32);
                var apn = ReadText(value, ref cursor, 48);
                End(value, cursor);
                return ($"{user},{password},{apn}", $"{user},{password},{apn}");
            case MainPowerReport:
                Length(value, 11);
                var type = ReportTypeOptions.FirstOrDefault(option => option.Key == value[0]);
                if (type.Value == null) throw new FormatException("无效的主电上报类型。");
                var values = value[0] == 5 ? new[] { value[1].ToString(CultureInfo.InvariantCulture) } : value[0] is 7 or 8
                    ? Enumerable.Range(0, 5).Select(i => $"{Decimal(value[i * 2 + 1]):D2}{Decimal(value[i * 2 + 2]):D2}")
                    : new[] { value[1].ToString(CultureInfo.InvariantCulture), Decimal(value[2]).ToString(), Decimal(value[3]).ToString(), Decimal(value[4]).ToString() };
                var parts = string.Join(",", values);
                return ($"{type.Value},{parts}", $"{value[0]},{parts}");
            case MeterBottom:
                Length(value, 6);
                var fraction = GasDataObjectValues.DecodeUnsigned(value[4..]);
                if (fraction > 9999) throw new FormatException("底码小数字段超过9999。");
                var bottom = N(GasDataObjectValues.DecodeUnsigned(value[..4]) + fraction / 10000m);
                return (bottom, bottom);
            case SettlementMode:
            case Compensation:
            case MeteringVerification:
            case PulseResolution:
                Length(value, 1); return (ChoiceText(code, value[0]), value[0].ToString(CultureInfo.InvariantCulture));
            case SettlementFunctions:
                Length(value, 1);
                return (string.Join(",", new[] { "Purge", "ShowSettlement", "VirtualBilling" }.Select((key, index) =>
                    Get(code).GetParameter(key).FormatValue(((value[0] & (1 << index)) != 0).ToString()))),
                    string.Join(",", Enumerable.Range(0, 3).Select(index => (value[0] >> index) & 1)));
            case MonthlyFreeze:
                Length(value, 3);
                var day = Decimal(value[0]); var hour = Decimal(value[1]); var minute = Decimal(value[2]);
                if (day > 28 || hour > 23 || minute > 59) throw new FormatException("冻结日期或时分无效。");
                return ($"{day},{hour},{minute}", $"{day},{hour},{minute}");
            case MeterSpecification:
                Length(value, 2);
                return ($"{ChoiceText(code, value[0], "Type")},{ChoiceText(code, value[1], "Specification")}", $"{value[0]},{value[1]}");
            case ConstantFlowThreshold:
                Length(value, 6);
                var flow = $"{N(GasDataObjectValues.DecodeUnsigned(value[..2], 3))},{N(GasDataObjectValues.DecodeUnsigned(value[2..4], 3))},{N(GasDataObjectValues.DecodeUnsigned(value[4..]))}";
                return (flow, flow);
            case MainVoltage:
            case MeteringVoltage:
                Length(value, 1); var voltage = N(value[0] / 10m); return (voltage, voltage);
            case ExternalPressure:
                Length(value, 2); var pressure = N(GasDataObjectValues.DecodeUnsigned(value, 1)); return (pressure, pressure);
            case MeteringVersion:
            case BaseMeterNumber:
                var textOffset = 0; var text = ReadText(value, ref textOffset, 32); End(value, textOffset); return (text, text);
            case ValveStatus:
                Length(value, 8);
                var valveState = (byte)((value[0] >> 4) & 7);
                return (ChoiceText(code, valveState), valveState.ToString(CultureInfo.InvariantCulture));
            case ModemStatus:
                Length(value, 8); var modem = value[0] & 15;
                string[] modemStates = ["开机失败", "SIM卡异常", "信号搜寻失败", "网络注册失败", "平台注册失败", "上报失败", "上报成功"];
                return (modem < modemStates.Length ? modemStates[modem] : $"未知状态({modem})", modem.ToString(CultureInfo.InvariantCulture));
            case NetworkOperator:
                var imsi = FixedText(value, 15).Item1;
                if (imsi.Length < 5) throw new FormatException("IMSI缺少有效运营商前缀。");
                var provider = imsi[..5] switch
                {
                    "46000" or "46002" or "46004" or "46007" or "46008" or "46024" or "46013" => 0,
                    "46001" or "46006" or "46009" => 1,
                    "46003" or "46005" or "46011" => 2,
                    _ => -1
                };
                return (provider < 0 ? $"未知运营商({imsi[..5]})" : ChoiceText(code, (byte)provider), provider.ToString(CultureInfo.InvariantCulture));
            case CommunicationSelfCheck:
                Length(value, 2);
                var status = value[1] switch { 0 => "自检正常", 1 => "无SIM卡或硬件异常", 2 => "设备注册失败", 3 => "平台无响应", 0xFF => "自检未完成", _ => $"未知结果(0x{value[1]:X2})" };
                return ($"0x{value[0]:X2},{status}", $"{value[0]},{value[1]}");
            default: throw new InvalidOperationException($"未登记的设备项目：{code}。");
        }
    }

    /// <summary>
    /// 编码有长度前缀的ASCII字段，拒绝静默替换中文和超出协议上限的内容
    /// </summary>
    private static byte[] Prefix(string value, int maximum, bool allowEmpty = true)
    {
        if ((!allowEmpty && value.Length == 0) || value.Length > maximum || value.Any(character => character > 0x7F))
            throw new FormatException($"设备文本必须为{(allowEmpty ? "0" : "1")}至{maximum}字节ASCII。");
        return [(byte)value.Length, .. Encoding.ASCII.GetBytes(value)];
    }

    /// <summary>
    /// 按长度前缀读取一个文本字段，返回实际消费位置
    /// </summary>
    private static string ReadText(byte[] value, ref int index, int maximum)
    {
        if (index >= value.Length) throw new FormatException("设备文本缺少长度字段。");
        var length = value[index++];
        if (length > maximum || index + length > value.Length) throw new FormatException("设备文本长度与实际内容不符。");
        var text = Ascii(value.AsSpan(index, length));
        index += length;
        return text;
    }

    /// <summary>
    /// 固定宽度模块标识只去除末尾00/FF填充，实际文本仍严格按ASCII解码
    /// </summary>
    private static (string, string) FixedText(byte[] value, int length)
    {
        Length(value, length);
        var used = value.Length;
        while (used > 0 && value[used - 1] is 0 or 0xFF) used--;
        if (used == 0) throw new FormatException("设备标识为空或全为填充字节。");
        var text = Ascii(value.AsSpan(0, used));
        return (text, text);
    }

    /// <summary>
    /// 明确拒绝非ASCII回包，避免转换为问号后误判成功
    /// </summary>
    private static string Ascii(ReadOnlySpan<byte> value)
    {
        foreach (var character in value) if (character > 0x7F) throw new FormatException("设备返回非ASCII文本。");
        return Encoding.ASCII.GetString(value);
    }

    /// <summary>
    /// 固定对象长度不包含帧头、对象个数和对象编号
    /// </summary>
    private static void Length(byte[] value, int length)
    {
        if (value.Length != length) throw new FormatException($"设备对象应为{length}字节，实际{value.Length}字节。");
    }

    /// <summary>
    /// 变长对象必须完整消费，尾部数据不能丢弃后判成功
    /// </summary>
    private static void End(byte[] value, int index)
    {
        if (value.Length != index) throw new FormatException("设备对象尾部存在多余字节。");
    }

    /// <summary>
    /// 表具时钟使用六字节BCD，不经过字符串或UTC转换
    /// </summary>
    private static byte[] EncodeTime(DateTime value) =>
        [Bcd(value.Year % 100), Bcd(value.Month), Bcd(value.Day), Bcd(value.Hour), Bcd(value.Minute), Bcd(value.Second)];

    /// <summary>
    /// 核对BCD及实际日历范围，错误时间作为本项失败
    /// </summary>
    private static DateTime ReadTime(byte[] value)
    {
        Length(value, 6);
        return new DateTime(2000 + Decimal(value[0]), Decimal(value[1]), Decimal(value[2]), Decimal(value[3]), Decimal(value[4]), Decimal(value[5]));
    }

    /// <summary>
    /// 单个BCD分量的编码
    /// </summary>
    private static byte Bcd(int value) => (byte)((value / 10 << 4) | value % 10);

    /// <summary>
    /// 单个BCD分量的解码，非法半字节不能作为有效数字
    /// </summary>
    private static int Decimal(byte value) => (value & 15) <= 9 && (value >> 4) <= 9
        ? (value >> 4) * 10 + (value & 15) : throw new FormatException("设备返回无效的BCD值。");

    private static string ChoiceText(string code, byte value, string parameter = SubParameterCodes.Value) =>
        Get(code).GetParameter(parameter).Options.FirstOrDefault(option => option.Key == value).Value ?? $"未知({value})";
}
