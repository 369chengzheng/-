using IMDP.Domain.Products;
using IMDP.Infrastructure.Protocol;

namespace IMDP.Products.GasDG.WholeA.Protocol;

/// <summary>
/// 固件升级协议解析器
/// </summary>
public class WholeAFirmwareUpgradeParser : FrameParserBase
{
    public override ProtocolIdentifier ProtocolId => new()
    {
        Name = "Upgrade_GasDG_Bootloader",
        Version = "1.0",
        Description = "固件升级协议"
    };

    public override FrameFormatConfig FrameFormat => new()
    {
        FrameHeader = new byte[] { 0x55, 0xAA },
        AddressField = new FieldConfig { Offset = 2, Length = 1 },
        CommandField = new FieldConfig { Offset = 3, Length = 1 },
        LengthField = new FieldConfig { Offset = 4, Length = 2 },
        ChecksumType = ChecksumType.Crc16Ccitt
    };

    public override string Description => "燃气产品A固件升级协议";
}
