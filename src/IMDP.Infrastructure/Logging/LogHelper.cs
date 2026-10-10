// =============================================================================
// 文件名：LogHelper.cs
// 功能描述：轻量 NLog 日志器工厂。
//           仅负责获得 ILogger 实例，不转发 Info/Error 等方法（避免包一层无价值中间层），
//           从而保留每个类独立的 logger 名，便于 nlog.config 按来源过滤。
// =============================================================================
using NLog;

namespace IMDP.Infrastructure.Logging;

/// <summary>
/// 轻量日志器工厂
/// <para>用法：<c>private static readonly ILogger _logger = LogHelper.For&lt;Xxx&gt;();</c></para>
/// <para>注意：不提供 Current()，因为 GetCurrentClassLogger() 在封装方法内部会返回
/// LogHelper 自身的 logger，语义不正确。</para>
/// </summary>
public static class LogHelper
{
    /// <summary>
    /// 获取指定类型的日志器（logger 名称为类型全名）。
    /// </summary>
    /// <typeparam name="T">使用该日志器的类型。</typeparam>
    /// <returns>NLog 日志器实例。</returns>
    public static ILogger For<T>() => LogManager.GetLogger(typeof(T).FullName);
}
