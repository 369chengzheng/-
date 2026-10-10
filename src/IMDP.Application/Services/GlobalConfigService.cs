// =============================================================================
// 文件名：GlobalConfigService.cs
// 功能描述：全局配置服务实现，封装串口参数、协议命令列表、当前物料号的读取与持久化。
//           全局配置以单行记录形式存储，使用固定 Id（1）标识。
// =============================================================================
using IMDP.Application.Services;
using IMDP.Domain.Entities;
using IMDP.Domain.Interfaces;
using IMDP.Domain.ValueObjects;
using Newtonsoft.Json;

namespace IMDP.Application.Services;

/// <summary>
/// 全局配置服务实现
/// <para>实现 <see cref="IGlobalConfigService"/>，依赖 <see cref="IGlobalConfigRepository"/> 读写 GlobalConfig 表。</para>
/// <para>全局配置在表中仅保留一条记录，使用固定 Id 标识；首次保存时 INSERT，后续 UPDATE。</para>
/// </summary>
public class GlobalConfigService : IGlobalConfigService
{
    /// <summary>全局配置仓储，用于读写 GlobalConfig 表</summary>
    private readonly IGlobalConfigRepository _configRepository;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="configRepository">全局配置仓储（由 DI 注入）</param>
    public GlobalConfigService(IGlobalConfigRepository configRepository)
    {
        _configRepository = configRepository;
    }

    /// <inheritdoc/>
    public async Task<GlobalConfig?> GetConfigAsync()
    {
        return await _configRepository.GetFirstAsync();
    }

    /// <inheritdoc/>
    public async Task<SerialPortConfig> GetSerialPortConfigAsync()
    {
        var config = await _configRepository.GetFirstAsync();
        if (config == null) return new SerialPortConfig();

        return new SerialPortConfig
        {
            BaudRate = config.BaudRate,
            DataBits = config.DataBits,
            StopBits = config.StopBits,
            Parity = config.Parity
        };
    }

    /// <inheritdoc/>
    public async Task SaveSerialPortConfigAsync(SerialPortConfig config)
    {
        var entity = await _configRepository.GetFirstAsync();
        if (entity == null)
        {
            // 首次保存：以固定 Id 创建新记录
            entity = new GlobalConfig { Id = 1L };
            entity.BaudRate = config.BaudRate;
            entity.DataBits = config.DataBits;
            entity.StopBits = config.StopBits;
            entity.Parity = config.Parity;
            entity.UpdatedTime = DateTime.Now;
            await _configRepository.AddAsync(entity);
        }
        else
        {
            // 后续保存：更新已有记录
            entity.BaudRate = config.BaudRate;
            entity.DataBits = config.DataBits;
            entity.StopBits = config.StopBits;
            entity.Parity = config.Parity;
            entity.UpdatedTime = DateTime.Now;
            await _configRepository.UpdateAsync(entity);
        }
    }

    /// <inheritdoc/>
    public async Task<ProtocolConfig> GetProtocolConfigAsync()
    {
        var config = await _configRepository.GetFirstAsync();
        if (config == null || string.IsNullOrEmpty(config.CommandsJson))
            return new ProtocolConfig();

        var commands = JsonConvert.DeserializeObject<List<ProtocolCommand>>(config.CommandsJson);
        return new ProtocolConfig { Commands = commands ?? new List<ProtocolCommand>() };
    }

    /// <inheritdoc/>
    public async Task SaveProtocolConfigAsync(ProtocolConfig config)
    {
        var entity = await _configRepository.GetFirstAsync();
        if (entity == null)
        {
            // 首次保存：以固定 Id 创建新记录
            entity = new GlobalConfig { Id = 1L };
            entity.CommandsJson = JsonConvert.SerializeObject(config.Commands);
            entity.UpdatedTime = DateTime.Now;
            await _configRepository.AddAsync(entity);
        }
        else
        {
            // 后续保存：更新已有记录
            entity.CommandsJson = JsonConvert.SerializeObject(config.Commands);
            entity.UpdatedTime = DateTime.Now;
            await _configRepository.UpdateAsync(entity);
        }
    }

    /// <inheritdoc/>
    public async Task<string> GetMaterialNumberAsync()
    {
        var config = await _configRepository.GetFirstAsync();
        return config?.MaterialNumber ?? string.Empty;
    }

    /// <inheritdoc/>
    public async Task SetMaterialNumberAsync(string materialNumber)
    {
        var entity = await _configRepository.GetFirstAsync();
        if (entity == null)
        {
            // 首次保存：以固定 Id 创建新记录
            entity = new GlobalConfig { Id = 1L, MaterialNumber = materialNumber };
            entity.UpdatedTime = DateTime.Now;
            await _configRepository.AddAsync(entity);
        }
        else
        {
            // 后续保存：更新已有记录
            entity.MaterialNumber = materialNumber;
            entity.UpdatedTime = DateTime.Now;
            await _configRepository.UpdateAsync(entity);
        }
    }
}