using IMDP.Domain.Products;
using IMDP.Infrastructure.Protocol;

namespace IMDP.Products.GasDG.WholeA.Protocol;

/// <summary>
/// 国内燃气整机A主协议解析器
/// 帧格式: [AA 55] [Addr] [Cmd] [Len] [Data...] [CRC16]
/// </summary>
public class WholeAMainProtocolParser : FrameParserBase
{
    public override ProtocolIdentifier ProtocolId => new()
    {
        Name = "Main_GasDG_Standard",
        Version = "1.0",
        Description = "国内燃气整机A主协议"
    };

    public override FrameFormatConfig FrameFormat => new()
    {
        FrameHeader = new byte[] { 0xAA, 0x55 },
        AddressField = new FieldConfig { Offset = 2, Length = 1 },
        CommandField = new FieldConfig { Offset = 3, Length = 1 },
        LengthField = new FieldConfig { Offset = 4, Length = 1 },
        ChecksumType = ChecksumType.Crc16Modbus
    };

    public override string Description => "燃气产品A标准通讯协议";
}
