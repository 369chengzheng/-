// =============================================================================
// 文件名：IGlobalConfigService.cs
// 功能描述：全局配置服务接口（领域层契约）
//           定义对系统全局配置（串口、协议、物料号）的读写契约。
//           全局配置以单行记录形式存储在 GlobalConfig 表，使用固定 Id 标识。
//           此接口定义于 Domain 层以避免 Infrastructure 反向依赖 Application 层。
// =============================================================================
using IMDP.Domain.Entities;
using IMDP.Domain.ValueObjects;

namespace IMDP.Domain.Interfaces;

/// <summary>
/// 全局配置服务接口
/// <para>封装系统级全局配置（串口参数、协议命令列表、当前物料号）的读取与持久化操作。</para>
/// <para>实现类为 <see cref="IMDP.Application.Services.GlobalConfigService"/>，底层依赖 <see cref="IGlobalConfigRepository"/>。</para>
/// </summary>
public interface IGlobalConfigService
{
    /// <summary>
    /// 获取全局配置实体
    /// <para>返回 GlobalConfig 表的首条（唯一）记录，不存在则返回 null。</para>
    /// </summary>
    /// <returns>全局配置实体；未初始化时返回 null</returns>
    Task<GlobalConfig?> GetConfigAsync();

    /// <summary>
    /// 获取串口配置
    /// <para>从全局配置中提取串口参数（波特率、数据位、停止位、校验位）。</para>
    /// </summary>
    /// <returns>串口配置；全局配置不存在时返回默认实例</returns>
    Task<SerialPortConfig> GetSerialPortConfigAsync();

    /// <summary>
    /// 保存串口配置
    /// <para>全局配置不存在时创建新记录（使用固定 Id），存在时更新串口字段。</para>
    /// </summary>
    /// <param name="config">待保存的串口配置</param>
    /// <returns>表示异步操作的任务</returns>
    Task SaveSerialPortConfigAsync(SerialPortConfig config);

    /// <summary>
    /// 获取协议配置
    /// <para>从全局配置的 CommandsJson 字段反序列化得到协议命令列表。</para>
    /// </summary>
    /// <returns>协议配置；无数据时返回空命令列表的实例</returns>
    Task<ProtocolConfig> GetProtocolConfigAsync();

    /// <summary>
    /// 保存协议配置
    /// <para>将协议命令列表序列化为 JSON 写入全局配置的 CommandsJson 字段。</para>
    /// </summary>
    /// <param name="config">待保存的协议配置</param>
    /// <returns>表示异步操作的任务</returns>
    Task SaveProtocolConfigAsync(ProtocolConfig config);

    /// <summary>
    /// 获取当前物料号
    /// <para>物料号决定当前激活的产品插件与功能菜单。</para>
    /// </summary>
    /// <returns>当前物料号字符串；未设置时返回空字符串</returns>
    Task<string> GetMaterialNumberAsync();

    /// <summary>
    /// 设置当前物料号
    /// <para>更新全局配置中的物料号字段，通常会触发 <see cref="IMDP.Application.Events.MaterialNumberChangedEvent"/>。</para>
    /// </summary>
    /// <param name="materialNumber">新的物料号</param>
    /// <returns>表示异步操作的任务</returns>
    Task SetMaterialNumberAsync(string materialNumber);
}
