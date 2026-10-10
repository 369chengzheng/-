// =============================================================================
// 文件名：StationService.cs
// 功能描述：工位管理服务实现，维护检测工位的内存状态集合与计时器。
//           工位数据不持久化，应用重启后清空；支持工位的增删改、计时控制与状态切换。
// =============================================================================
using System.Collections.ObjectModel;
using IMDP.Application.DTOs;
using IMDP.Application.Services;

namespace IMDP.Application.Services;

/// <summary>
/// 工位管理服务实现
/// <para>实现 <see cref="IStationService"/>，使用 <see cref="ObservableCollection{StationDto}"/> 维护工位列表。</para>
/// <para>每个工位关联一个 <see cref="System.Diagnostics.Stopwatch"/> 计时器，用于记录检测耗时。</para>
/// <para>工位状态流转：Idle（空闲）→ Testing（测试中）→ Completed（已完成）→ Reset → Idle。</para>
/// </summary>
public class StationService : IStationService
{
    /// <summary>工位集合（ObservableCollection 支持 UI 绑定变更通知）</summary>
    private readonly ObservableCollection<StationDto> _stations = new();

    /// <summary>工位计时器字典（键为工位 Id，值为 Stopwatch）</summary>
    private readonly Dictionary<int, System.Diagnostics.Stopwatch> _timers = new();

    /// <summary>下一个工位 Id（内存自增，从 1 开始）</summary>
    private int _nextId = 1;

    /// <inheritdoc/>
    public IReadOnlyList<StationDto> GetStations() => _stations.ToList().AsReadOnly();

    /// <inheritdoc/>
    public StationDto GetStation(int stationId)
    {
        return _stations.FirstOrDefault(s => s.Id == stationId)
            ?? throw new ArgumentException($"工位 {stationId} 不存在");
    }

    /// <inheritdoc/>
    public void AddStation(string portName)
    {
        var station = new StationDto
        {
            Id = _nextId++,
            Name = $"工位{_nextId - 1}",
            SerialPort = portName,
            Status = "Idle"
        };
        _stations.Add(station);
        // 为新工位创建独立的计时器
        _timers[station.Id] = new System.Diagnostics.Stopwatch();
    }

    /// <inheritdoc/>
    public void RemoveStation(int stationId)
    {
        var station = _stations.FirstOrDefault(s => s.Id == stationId);
        if (station != null)
        {
            _stations.Remove(station);
            _timers.Remove(stationId);
        }
    }

    /// <inheritdoc/>
    public void SetStationPort(int stationId, string portName)
    {
        var station = GetStation(stationId);
        station.SerialPort = portName;
    }

    /// <inheritdoc/>
    public void SetStationProductSn(int stationId, string productSn)
    {
        var station = GetStation(stationId);
        station.ProductSn = productSn;
    }

    /// <inheritdoc/>
    public void StartTimer(int stationId)
    {
        if (_timers.TryGetValue(stationId, out var timer))
        {
            timer.Restart();
            var station = GetStation(stationId);
            station.Status = "Testing";
        }
    }

    /// <inheritdoc/>
    public void StopTimer(int stationId)
    {
        if (_timers.TryGetValue(stationId, out var timer))
        {
            timer.Stop();
            var station = GetStation(stationId);
            station.Status = "Completed";
        }
    }

    /// <inheritdoc/>
    public TimeSpan GetElapsed(int stationId)
    {
        if (_timers.TryGetValue(stationId, out var timer))
            return timer.Elapsed;
        return TimeSpan.Zero;
    }

    /// <inheritdoc/>
    public void ResetStation(int stationId)
    {
        var station = GetStation(stationId);
        station.ProductSn = string.Empty;
        station.ParameterValues.Clear();
        station.Status = "Idle";
        if (_timers.TryGetValue(stationId, out var timer))
            timer.Reset();
    }

    /// <inheritdoc/>
    public void ClearAll()
    {
        _stations.Clear();
        _timers.Clear();
        _nextId = 1;
    }

    /// <inheritdoc/>
    public int ActiveCount => _stations.Count(s => s.Status == "Testing");
}