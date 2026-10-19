// =============================================================================
// 文件名：CommandCatalog.cs
// 功能描述：命令码注册表。
//           每条指令登记「命令码 → 组数据区 + 解响应」，供 SerialSession 按码分发：
//           发送方只给命令码（可选参数），注册表负责组帧负载与响应解析，
//           大量指令不再逐条手写组帧/解析代码。
// =============================================================================
using IMDP.Domain.Products;

namespace IMDP.Infrastructure.Protocol;

/// <summary>单条指令定义（命令码 → 组负载 + 解响应）。</summary>
public sealed class CommandDefinition
{
    /// <summary>命令码（数据区首字节，如 0x63；单命令码帧则为命令字段值，如 0xD0）。</summary>
    public byte Code { get; init; }

    /// <summary>指令名称（日志/UI 展示用）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 组请求负载：返回完整数据区（含命令码首字节，如 63 55 00）。
    /// <para>为 null 时表示该命令数据区即 [Code] 单字节。</para>
    /// </summary>
    public Func<byte[]>? BuildPayload { get; init; }

    /// <summary>解响应：把帧解析结果转为业务值（null 表示不解析，原样返回）。</summary>
    public Func<FrameParseResult, object?>? Parse { get; init; }

    /// <summary>本命令专属帧格式变体（可空=沿用会话默认格式；如 0xD0 需 CommandIsPayload=true）。</summary>
    public FrameFormatConfig? FormatOverride { get; init; }

    /// <summary>生成实际数据区：BuildPayload 为空时退化为 [Code]。</summary>
    public byte[] Build() => BuildPayload != null ? BuildPayload() : new byte[] { Code };
}

/// <summary>
/// 命令码注册表
/// <para>按命令码登记/查找 CommandDefinition；同一码重复注册时后者覆盖前者。
/// 典型用法：产品模块启动时把本协议全部指令注册到静态表，业务层只按码调用。</para>
/// </summary>
public sealed class CommandCatalog
{
    private readonly Dictionary<byte, CommandDefinition> _commands = new();

    /// <summary>注册一条指令（同码覆盖）。</summary>
    public void Register(CommandDefinition command) => _commands[command.Code] = command;

    /// <summary>按码查找指令定义；未注册返回 null。</summary>
    public CommandDefinition? Find(byte code) => _commands.TryGetValue(code, out var cmd) ? cmd : null;

    /// <summary>已注册的全部指令（日志/诊断）。</summary>
    public IReadOnlyCollection<CommandDefinition> All => _commands.Values;
}
