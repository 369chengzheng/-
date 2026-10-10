using IMDP.Domain.Products;
using IMDP.Infrastructure.Protocol;
using IMDP.Infrastructure.Serial;
using IMDP.Domain.Interfaces;
using IMDP.Products.GasDG.WholeA.Protocol;

namespace IMDP.Products.GasDG.WholeA.Services;

/// <summary>
/// 产品A检测服务
/// </summary>
public class WholeADetectService
{
    private readonly ISerialService _serialService;
    private readonly IProtocolManager _protocolManager;
    private readonly IFrameParser _mainParser;

    public WholeADetectService(ISerialService serialService, IProtocolManager protocolManager)
    {
        _serialService = serialService;
        _protocolManager = protocolManager;
        _mainParser = new WholeAMainProtocolParser();
        _protocolManager.RegisterParser(_mainParser);
        _protocolManager.RegisterParser(new WholeAFirmwareUpgradeParser());
    }

    /// <summary>
    /// 读取设备参数
    /// </summary>
    public async Task<Dictionary<string, string>?> ReadParametersAsync(string portName, byte deviceAddress, int timeoutMs = 3000)
    {
        _protocolManager.SwitchProtocol(_mainParser.ProtocolId);

        var command = new byte[] { 0x02 };
        var frame = _mainParser.BuildFrame(deviceAddress, command);

        var response = await _serialService.SendAndWaitAsync(portName, frame, timeoutMs, 1, 0);
        if (response == null) return null;

        var result = _mainParser.TryParseFrame(response, 0, response.Length);
        if (!result.IsComplete || result.Data == null) return null;

        return ParseParameterData(result.Data);
    }

    /// <summary>
    /// 写入设备参数
    /// </summary>
    public async Task<bool> WriteParametersAsync(string portName, byte deviceAddress, Dictionary<string, string> parameters, int timeoutMs = 3000)
    {
        _protocolManager.SwitchProtocol(_mainParser.ProtocolId);

        var data = BuildParameterData(parameters);
        var command = new byte[data.Length + 1];
        command[0] = 0x01;
        Array.Copy(data, 0, command, 1, data.Length);

        var frame = _mainParser.BuildFrame(deviceAddress, command);
        var response = await _serialService.SendAndWaitAsync(portName, frame, timeoutMs, 1, 0);
        return response != null;
    }

    private static Dictionary<string, string> ParseParameterData(byte[] data)
    {
        var result = new Dictionary<string, string>();
        var paramNames = new[] { "IP地址", "端口", "闲置天数", "表具规格", "域名", "一级不通讯天数", "一级不用气天数", "大流阈值", "小流阈值" };

        for (int i = 0; i < data.Length && i < paramNames.Length; i++)
        {
            result[paramNames[i]] = data[i].ToString();
        }

        return result;
    }

    private static byte[] BuildParameterData(Dictionary<string, string> parameters)
    {
        var data = new byte[parameters.Count];
        int i = 0;
        foreach (var param in parameters.Values)
        {
            if (byte.TryParse(param, out var value))
                data[i] = value;
            i++;
        }
        return data;
    }
}
