using System.IO;
using System.Text;

namespace IMDP.Products.GasDG.WholeA.Protocol;

/// <summary>
/// 威星协议命令帧构建器
/// <para>
/// 封装协议文档第 3 章"命令帧"中定义的全部 9 条命令的构建逻辑，
/// 每条命令生成完整的协议帧字节数组，可直接通过串口发送。
/// </para>
/// <para>
/// 命令列表：
/// <list type="table">
///   <listheader><term>数据标识</term><term>功能</term></listheader>
///   <item><term>0xC518</term><description>出厂配置参数 读/写</description></item>
///   <item><term>0xC519</term><description>加密平台信息 读/写</description></item>
///   <item><term>0xC620</term><description>配置参数 读/写</description></item>
///   <item><term>0xC525</term><description>网络参数及终端号 读</description></item>
///   <item><term>0xC527</term><description>设备运行信息 读</description></item>
///   <item><term>0xA027</term><description>蓝牙控制 写</description></item>
/// </list>
/// </para>
/// <example>
/// 示例：构建读取出厂配置参数命令并发送
/// <code>
/// var builder = new ViewshineCommandBuilder();
/// byte[] meterAddr = ViewshineProtocolParser.MeterNumberToBcd("0011234567890");
/// byte[] frame = builder.BuildReadFactoryConfigCommand(meterAddr);
/// serialPort.Write(frame, 0, frame.Length);
/// </code>
/// </example>
/// </summary>
public class ViewshineCommandBuilder
{
    private readonly ViewshineProtocolParser _parser;

    /// <summary>
    /// 初始化命令构建器
    /// </summary>
    /// <param name="parser">协议帧解析器实例（用于构建数据域和帧）</param>
    public ViewshineCommandBuilder(ViewshineProtocolParser? parser = null)
    {
        _parser = parser ?? new ViewshineProtocolParser();
    }

    // ===== 4.1 出厂配置参数 (0xC518) =====

    /// <summary>
    /// 构建读取出厂配置参数命令帧（控制码=0x01，数据标识=0xC518）
    /// <para>下行数据内容为空，表具应答时返回出厂配置参数。</para>
    /// </summary>
    /// <param name="meterAddress">表具地址（6 字节 BCD，低位在前）</param>
    /// <param name="deviceType">设备类型（默认民用物联网表）</param>
    /// <returns>完整的协议命令帧</returns>
    public byte[] BuildReadFactoryConfigCommand(byte[] meterAddress, DeviceType deviceType = DeviceType.CivilIotMeter)
    {
        byte controlCode = ViewshineProtocolConstants.BuildControlCode(
            DataDirection.MasterToMeter, FunctionCategory.ReadData);

        byte[] dataDomain = _parser.BuildDataDomain(
            DateTime.Now, deviceType,
            ViewshineProtocolConstants.DataIdFactoryConfig, 0, null);

        return _parser.BuildFrame(meterAddress, dataDomain, controlCode);
    }

    /// <summary>
    /// 构建写出厂配置参数命令帧（控制码=0x04，数据标识=0xC518）
    /// <para>根据 <paramref name="request"/> 中设置的"是否更改"标志序列化对应参数块。</para>
    /// </summary>
    /// <param name="meterAddress">表具地址（6 字节 BCD）</param>
    /// <param name="request">写出厂配置参数请求</param>
    /// <param name="deviceType">设备类型</param>
    /// <returns>完整的协议命令帧</returns>
    /// <example>
    /// <code>
    /// var request = new WriteFactoryConfigRequest
    /// {
    ///     ChangeMeterNumber = true,
    ///     MeterNumber = "0011234567890",
    ///     ChangeMeterTime = true,
    ///     MeterTime = DateTime.Now,
    ///     ChangeMeterSpec = true,
    ///     MeterSpec = MeterSpecification.G10
    /// };
    /// byte[] frame = builder.BuildWriteFactoryConfigCommand(meterAddr, request);
    /// </code>
    /// </example>
    public byte[] BuildWriteFactoryConfigCommand(byte[] meterAddress,WriteFactoryConfigRequest request,DeviceType deviceType = DeviceType.CivilIotMeter)
    {
        byte[] dataContent = SerializeWriteFactoryConfig(request);

        byte controlCode = ViewshineProtocolConstants.BuildControlCode(
            DataDirection.MasterToMeter, FunctionCategory.WriteData);

        byte[] dataDomain = _parser.BuildDataDomain(
            DateTime.Now, deviceType,
            ViewshineProtocolConstants.DataIdFactoryConfig, 0, dataContent);

        return _parser.BuildFrame(meterAddress, dataDomain, controlCode);
    }

    // ===== 4.2 加密平台信息 (0xC519) =====

    /// <summary>
    /// 构建读取加密平台信息命令帧（控制码=0x01，数据标识=0xC519）
    /// </summary>
    /// <param name="meterAddress">表具地址</param>
    /// <param name="deviceType">设备类型</param>
    /// <returns>完整的协议命令帧</returns>
    public byte[] BuildReadEncryptionInfoCommand(byte[] meterAddress, DeviceType deviceType = DeviceType.CivilIotMeter)
    {
        byte controlCode = ViewshineProtocolConstants.BuildControlCode(
            DataDirection.MasterToMeter, FunctionCategory.ReadData);

        byte[] dataDomain = _parser.BuildDataDomain(
            DateTime.Now, deviceType,
            ViewshineProtocolConstants.DataIdEncryptionInfo, 0, null);

        return _parser.BuildFrame(meterAddress, dataDomain, controlCode);
    }

    /// <summary>
    /// 构建设置加密平台信息命令帧（控制码=0x04，数据标识=0xC519）
    /// <para>根据 <paramref name="request"/> 中设置的"是否更改"标志序列化对应参数块。</para>
    /// <para>协议 4.2.1 节定义的字段顺序：</para>
    /// <para>表具编号 → 加密密钥 → 认证密钥 → Onenet平台参数 → PSM模式 →</para>
    /// <para>反向累积量 → 表型号+生产状态+配置开关+关阀类型 → K/B值 → 网络类型 → 预留</para>
    /// </summary>
    /// <param name="meterAddress">表具地址（6 字节 BCD）</param>
    /// <param name="request">设置加密及平台信息请求</param>
    /// <param name="deviceType">设备类型</param>
    /// <returns>完整的协议命令帧</returns>
    /// <example>
    /// <code>
    /// var request = new WriteEncryptionInfoRequest
    /// {
    ///     ChangeMeterNumber = true,
    ///     MeterNumber = "11223344556677880000",
    ///     ChangeEncryptionKey = true,
    ///     EncryptionKeyLength = 24,
    ///     EncryptionKey = new byte[32],
    ///     ChangePsmMode = true,
    ///     PsmMode = 1
    /// };
    /// byte[] frame = builder.BuildWriteEncryptionInfoCommand(meterAddr, request);
    /// </code>
    /// </example>
    public byte[] BuildWriteEncryptionInfoCommand(byte[] meterAddress, WriteEncryptionInfoRequest request,
        DeviceType deviceType = DeviceType.CivilIotMeter)
    {
        byte[] dataContent = SerializeWriteEncryptionInfo(request);

        byte controlCode = ViewshineProtocolConstants.BuildControlCode(
            DataDirection.MasterToMeter, FunctionCategory.WriteData);

        byte[] dataDomain = _parser.BuildDataDomain(
            DateTime.Now, deviceType,
            ViewshineProtocolConstants.DataIdEncryptionInfo, 0, dataContent);

        return _parser.BuildFrame(meterAddress, dataDomain, controlCode);
    }

    // ===== 4.3 配置参数 (0xC620) =====

    /// <summary>
    /// 构建读取配置参数命令帧（控制码=0x01，数据标识=0xC620）
    /// </summary>
    /// <param name="meterAddress">表具地址</param>
    /// <param name="deviceType">设备类型</param>
    /// <returns>完整的协议命令帧</returns>
    public byte[] BuildReadConfigParamsCommand(byte[] meterAddress, DeviceType deviceType = DeviceType.CivilIotMeter)
    {
        byte controlCode = ViewshineProtocolConstants.BuildControlCode(
            DataDirection.MasterToMeter, FunctionCategory.ReadData);

        byte[] dataDomain = _parser.BuildDataDomain(
            DateTime.Now, deviceType,
            ViewshineProtocolConstants.DataIdConfigParams, 0, null);

        return _parser.BuildFrame(meterAddress, dataDomain, controlCode);
    }

    // ===== 4.4 网络参数及终端号 (0xC525) =====

    /// <summary>
    /// 构建读取网络参数及终端号命令帧（控制码=0x01，数据标识=0xC525）
    /// <para>上行返回 IMEI、IMSI、ICCID、CSQ、RSRP、SNR、ECL、CellID 等网络信息。</para>
    /// </summary>
    /// <param name="meterAddress">表具地址</param>
    /// <param name="deviceType">设备类型</param>
    /// <returns>完整的协议命令帧</returns>
    public byte[] BuildReadNetworkParamsCommand(byte[] meterAddress, DeviceType deviceType = DeviceType.CivilIotMeter)
    {
        byte controlCode = ViewshineProtocolConstants.BuildControlCode(
            DataDirection.MasterToMeter, FunctionCategory.ReadData);

        byte[] dataDomain = _parser.BuildDataDomain(
            DateTime.Now, deviceType,
            ViewshineProtocolConstants.DataIdNetworkParams, 0, null);

        return _parser.BuildFrame(meterAddress, dataDomain, controlCode);
    }

    // ===== 4.5 设备运行信息 (0xC527) =====

    /// <summary>
    /// 构建读取设备运行信息命令帧（控制码=0x01，数据标识=0xC527）
    /// <para>上行返回报警器状态、RS485 接口状态、蓝牙连接状态等。</para>
    /// </summary>
    /// <param name="meterAddress">表具地址</param>
    /// <param name="deviceType">设备类型</param>
    /// <returns>完整的协议命令帧</returns>
    public byte[] BuildReadDeviceInfoCommand(byte[] meterAddress, DeviceType deviceType = DeviceType.CivilIotMeter)
    {
        byte controlCode = ViewshineProtocolConstants.BuildControlCode(
            DataDirection.MasterToMeter, FunctionCategory.ReadData);

        byte[] dataDomain = _parser.BuildDataDomain(
            DateTime.Now, deviceType,
            ViewshineProtocolConstants.DataIdDeviceInfo, 0, null);

        return _parser.BuildFrame(meterAddress, dataDomain, controlCode);
    }

    // ===== 4.6 蓝牙控制 (0xA027) =====

    /// <summary>
    /// 构建蓝牙控制命令帧（控制码=0x04，数据标识=0xA027）
    /// <para>
    /// 数据内容：操作(1B) + 表号(5B)
    /// <list type="bullet">
    ///   <item>操作 0=关闭蓝牙, 1=打开蓝牙, 2=搜索设备</item>
    ///   <item>表号在操作为 2（搜索设备）时使用，针对主机模式设备</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="meterAddress">表具地址</param>
    /// <param name="request">蓝牙控制请求参数</param>
    /// <param name="deviceType">设备类型</param>
    /// <returns>完整的协议命令帧</returns>
    /// <example>
    /// <code>
    /// var request = new BluetoothControlRequest
    /// {
    ///     Operation = BluetoothOperation.Open
    /// };
    /// byte[] frame = builder.BuildBluetoothControlCommand(meterAddr, request);
    /// </code>
    /// </example>
    public byte[] BuildBluetoothControlCommand(byte[] meterAddress,BluetoothControlRequest request,DeviceType deviceType = DeviceType.CivilIotMeter)
    {
        // 数据内容: 操作(1B) + 表号(5B)
        var dataContent = new byte[6];
        dataContent[0] = (byte)request.Operation;

        if (request.TargetMeterNumber != null)
        {
            int copyLen = Math.Min(request.TargetMeterNumber.Length, 5);
            Array.Copy(request.TargetMeterNumber, 0, dataContent, 1, copyLen);
        }

        byte controlCode = ViewshineProtocolConstants.BuildControlCode(
            DataDirection.MasterToMeter, FunctionCategory.WriteData);

        byte[] dataDomain = _parser.BuildDataDomain(
            DateTime.Now, deviceType,
            ViewshineProtocolConstants.DataIdBluetoothControl, 0, dataContent);

        return _parser.BuildFrame(meterAddress, dataDomain, controlCode);
    }

    // ===== 序列化方法 =====

    /// <summary>
    /// 序列化写出厂配置参数请求为数据内容字节数组
    /// <para>按照协议 4.1.1 节定义的顺序序列化各字段。</para>
    /// </summary>
    private static byte[] SerializeWriteFactoryConfig(WriteFactoryConfigRequest request)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // 是否恢复出厂设置
        writer.Write((byte)(request.ResetToFactory ? 1 : 0));

        // 是否更改表号
        writer.Write((byte)(request.ChangeMeterNumber ? 1 : 0));
        if (request.ChangeMeterNumber && request.MeterNumber != null)
        {
            byte[] bcd = ViewshineProtocolParser.MeterNumberToBcd(request.MeterNumber);
            writer.Write(bcd);
        }

        // 是否更改表具时间
        writer.Write((byte)(request.ChangeMeterTime ? 1 : 0));
        if (request.ChangeMeterTime && request.MeterTime.HasValue)
        {
            var dt = request.MeterTime.Value;
            writer.Write(DecimalToBcd(dt.Year - 2000));
            writer.Write(DecimalToBcd(dt.Month));
            writer.Write(DecimalToBcd(dt.Day));
            writer.Write(DecimalToBcd(dt.Hour));
            writer.Write(DecimalToBcd(dt.Minute));
            writer.Write(DecimalToBcd(dt.Second));
        }

        // 是否更改表具出厂参数
        writer.Write((byte)(request.ChangeFactoryParams ? 1 : 0));
        if (request.ChangeFactoryParams && request.FactoryParams != null)
        {
            writer.Write(request.FactoryParams);
        }

        // 是否更改表具底码
        writer.Write((byte)(request.ChangeBaseCode ? 1 : 0));
        if (request.ChangeBaseCode && request.BaseCode.HasValue)
        {
            writer.Write(ViewshineProtocolParser.WriteLittleEndianDecimal(request.BaseCode.Value, 2));
        }

        // 是否更改运行模式
        writer.Write((byte)(request.ChangeRunMode ? 1 : 0));
        if (request.ChangeRunMode && request.RunMode.HasValue)
        {
            writer.Write((byte)request.RunMode.Value);
            if (request.PrepaidAmount.HasValue)
            {
                writer.Write(ViewshineProtocolParser.WriteLittleEndianDecimal(request.PrepaidAmount.Value, 2));
            }
        }

        // 是否更改表存上限
        writer.Write((byte)(request.ChangeStorageLimit ? 1 : 0));
        if (request.ChangeStorageLimit && request.StorageLimit.HasValue)
        {
            writer.Write(ViewshineProtocolParser.WriteLittleEndianDecimal(request.StorageLimit.Value, 2));
        }

        // 是否更改域名
        writer.Write((byte)(request.ChangeDomain ? 1 : 0));
        if (request.ChangeDomain)
        {
            if (request.DomainEnableFlag.HasValue)
                writer.Write(request.DomainEnableFlag.Value);
            if (request.Domain != null)
            {
                writer.Write((byte)Math.Min(request.Domain.Length, 30));
                writer.Write(Encoding.ASCII.GetBytes(request.Domain));
            }
            if (request.DomainPort.HasValue)
            {
                writer.Write((byte)(request.DomainPort.Value & 0xFF));
                writer.Write((byte)((request.DomainPort.Value >> 8) & 0xFF));
            }
        }

        // 信息安全是否加密
        writer.Write((byte)(request.ChangeEncryption ? 1 : 0));
        if (request.ChangeEncryption && request.IsEncrypted.HasValue)
        {
            writer.Write((byte)(request.IsEncrypted.Value ? 1 : 0));
        }

        // 是否更改表具规格
        writer.Write((byte)(request.ChangeMeterSpec ? 1 : 0));
        if (request.ChangeMeterSpec && request.MeterSpec.HasValue)
        {
            writer.Write((byte)request.MeterSpec.Value);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// 序列化设置加密及平台信息请求为数据内容字节数组
    /// <para>按照协议 4.2.1 节定义的顺序序列化各字段。</para>
    /// <para>每个字段块以"是否更改"标志字节开头，值为 1 时紧跟参数数据，为 0 时省略数据。</para>
    /// </summary>
    private static byte[] SerializeWriteEncryptionInfo(WriteEncryptionInfoRequest request)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // 是否更改表具编号
        writer.Write((byte)(request.ChangeMeterNumber ? 1 : 0));
        if (request.ChangeMeterNumber && request.MeterNumber != null)
        {
            var ascii = Encoding.ASCII.GetBytes(request.MeterNumber);
            var padded = new byte[32];
            Array.Fill(padded, (byte)'0');
            Array.Copy(ascii, 0, padded, 0, Math.Min(ascii.Length, 32));
            writer.Write(padded);
        }

        // 是否更改加密密钥
        writer.Write((byte)(request.ChangeEncryptionKey ? 1 : 0));
        if (request.ChangeEncryptionKey && request.EncryptionKey != null)
        {
            writer.Write(request.EncryptionKeyLength);
            var key = new byte[32];
            Array.Copy(request.EncryptionKey, 0, key, 0, Math.Min(request.EncryptionKey.Length, 32));
            writer.Write(key);
        }

        // 是否更改认证密钥
        writer.Write((byte)(request.ChangeAuthKey ? 1 : 0));
        if (request.ChangeAuthKey && request.AuthKey != null)
        {
            writer.Write(request.AuthKeyLength);
            var key = new byte[32];
            Array.Copy(request.AuthKey, 0, key, 0, Math.Min(request.AuthKey.Length, 32));
            writer.Write(key);
        }

        // 是否更改 Onenet 平台参数
        writer.Write((byte)(request.ChangeOnenetParams ? 1 : 0));
        if (request.ChangeOnenetParams && request.OnenetParams != null)
        {
            writer.Write(SerializeOnenetParams(request.OnenetParams));
        }

        // 是否更改进入 PSM 模式
        writer.Write((byte)(request.ChangePsmMode ? 1 : 0));
        if (request.ChangePsmMode && request.PsmMode.HasValue)
        {
            writer.Write(request.PsmMode.Value);
        }

        // 是否更改反向累积量
        writer.Write((byte)(request.ChangeReverseAccumulation ? 1 : 0));
        if (request.ChangeReverseAccumulation && request.ReverseAccumulation.HasValue)
        {
            writer.Write(ViewshineProtocolParser.WriteLittleEndianDecimal(request.ReverseAccumulation.Value, 2));
        }

        // 是否更改表型号（含生产状态、配置开关、关阀类型）
        writer.Write((byte)(request.ChangeMeterModel ? 1 : 0));
        if (request.ChangeMeterModel && request.MeterModel != null)
        {
            writer.Write(request.MeterModel.MeterTypeCode);
            writer.Write(request.MeterModel.RangeCode);
            writer.Write((byte)(request.ProductionStatus ?? ProductionStatus.Factory));
            writer.Write(request.ConfigSwitch ?? 0);
            writer.Write(request.ValveCloseType ?? 0);
        }

        // 是否更改调齿比系数（K/B 值）
        writer.Write((byte)(request.ChangeKBValue ? 1 : 0));
        if (request.ChangeKBValue)
        {
            if (request.KValue.HasValue)
                writer.Write(ViewshineProtocolParser.WriteLittleEndianDecimal(request.KValue.Value, 6));
            else
                writer.Write(new byte[4]);

            if (request.BValue.HasValue)
                writer.Write(ViewshineProtocolParser.WriteLittleEndianDecimal(request.BValue.Value, 6));
            else
                writer.Write(new byte[4]);
        }

        // 是否更改通讯板的网络类型
        writer.Write((byte)(request.ChangeNetworkType ? 1 : 0));
        if (request.ChangeNetworkType && request.NetworkType.HasValue)
        {
            writer.Write((byte)request.NetworkType.Value);
        }

        // 预留 8 字节
        writer.Write(new byte[8]);

        return ms.ToArray();
    }

    /// <summary>
    /// 序列化 Onenet 平台参数为 15 字节数组
    /// <para>结构：引用ID(2B) + 对象标识(2B) + 实例标识(2B) + 资源标识上行(2B) +
    /// 资源标识下行(2B) + 数据格式类型(1B) + 结束标志(1B) + 回应ID(2B) + 平台类型(1B)</para>
    /// </summary>
    private static byte[] SerializeOnenetParams(OnenetPlatformParams param)
    {
        var bytes = new byte[15];
        int offset = 0;

        WriteLittleEndianUShort(bytes, ref offset, param.ReferenceId);
        WriteLittleEndianUShort(bytes, ref offset, param.ObjectId);
        WriteLittleEndianUShort(bytes, ref offset, param.InstanceId);
        WriteLittleEndianUShort(bytes, ref offset, param.ResourceIdUp);
        WriteLittleEndianUShort(bytes, ref offset, param.ResourceIdDown);
        bytes[offset++] = param.DataValueType;
        bytes[offset++] = param.Flag;
        WriteLittleEndianUShort(bytes, ref offset, param.AckId);
        bytes[offset++] = param.PlatformType;

        return bytes;
    }

    /// <summary>写入小端序无符号 16 位整数</summary>
    private static void WriteLittleEndianUShort(byte[] buffer, ref int offset, ushort value)
    {
        buffer[offset++] = (byte)(value & 0xFF);
        buffer[offset++] = (byte)((value >> 8) & 0xFF);
    }

    /// <summary>十进制数转 BCD 编码</summary>
    private static byte DecimalToBcd(int value)
        => (byte)(((value / 10) << 4) | (value % 10));
}
