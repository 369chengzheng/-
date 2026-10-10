// =============================================================================
// 文件名：ProtocolConfig.cs
// 功能描述：协议配置值对象，包含协议指令列表与单条指令的执行参数（超时、重试、延迟等）。
//           作为全局配置中快捷指令 JSON 的反序列化目标类型。
// =============================================================================
namespace IMDP.Domain.ValueObjects;

/// <summary>
/// 协议配置值对象
/// <para>承载一组协议指令的集合，对应全局配置中的 CommandsJson 字段。</para>
/// </summary>
public class ProtocolConfig
{
    /// <summary>协议指令列表（按 <see cref="ProtocolCommand.Order"/> 排序执行）</summary>
    public List<ProtocolCommand> Commands { get; set; } = new();
}

/// <summary>
/// 协议指令值对象
/// <para>描述单条指令的执行参数，包括指令类型、超时、重试次数与发送间隔。</para>
/// </summary>
public class ProtocolCommand
{
    /// <summary>指令类型（对应协议定义的功能分类编码）</summary>
    public int CommandType { get; set; }

    /// <summary>单次执行超时时间（毫秒，默认 3000）</summary>
    public int TimeoutMs { get; set; } = 3000;

    /// <summary>失败重试次数（默认 1 次）</summary>
    public int RetryCount { get; set; } = 1;

    /// <summary>发送前的延迟时间（毫秒，默认 0 表示不延迟）</summary>
    public int SendDelayMs { get; set; } = 0;

    /// <summary>执行顺序（数值越小越先执行，默认 0）</summary>
    public int Order { get; set; } = 0;
}