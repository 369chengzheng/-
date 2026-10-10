// =============================================================================
// 文件名：OrderSwitchRecordService.cs
// 功能描述：工单切换记录写入服务实现
// =============================================================================
using System.Text.Json;
using IMDP.Application.DTOs;
using IMDP.Domain.Entities;
using IMDP.Domain.Interfaces;

namespace IMDP.Application.Services;

/// <summary>
/// 工单切换记录写入服务实现
/// <para>新记录保存实际加载的菜单标题快照；旧记录中的枚举名称由查询 DTO 兼容显示。</para>
/// <para>ComputerName、UserName、AppName 由 SqlSugar AOP 自动填充</para>
/// </summary>
public sealed class OrderSwitchRecordService : IOrderSwitchRecordService
{
    private readonly IOrderSwitchRepository _orderSwitchRepository;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _stateLock = new();

    private string _lastOrderNumber = string.Empty;
    private string _lastMaterialNumber = string.Empty;
    private long _sessionVersion;

    public OrderSwitchRecordService(IOrderSwitchRepository orderSwitchRepository)
    {
        _orderSwitchRepository = orderSwitchRepository;
    }

    /// <inheritdoc/>
    public async Task<bool> RecordIfChangedAsync(
        string orderNumber,
        string materialNumber,
        IReadOnlyList<ProductMenuItemDto> loadedMenus,
        string createdBy,
        DateTime createdTime)
    {
        orderNumber = orderNumber?.Trim() ?? string.Empty;
        materialNumber = materialNumber?.Trim() ?? string.Empty;
        createdBy = createdBy?.Trim() ?? string.Empty;

        if (orderNumber.Length == 0
            || materialNumber.Length == 0
            || createdBy.Length == 0
            || loadedMenus is null
            || loadedMenus.Count == 0)
        {
            return false;
        }

        long requestedSessionVersion;
        lock (_stateLock)
        {
            requestedSessionVersion = _sessionVersion;
            if (IsSameActiveOrder(orderNumber, materialNumber))
            {
                return false;
            }
        }

        await _writeGate.WaitAsync();
        try
        {
            // 等待写锁期间可能已由另一查询写入，或登录会话已经重置，因此必须二次判断
            lock (_stateLock)
            {
                if (requestedSessionVersion != _sessionVersion
                    || IsSameActiveOrder(orderNumber, materialNumber))
                {
                    return false;
                }
            }

            var menuNames = loadedMenus
                .DistinctBy(menu => menu.ModuleType)
                .Select(menu => menu.DisplayName)
                .ToList();

            await _orderSwitchRepository.AddAsync(new OrderSwitch
            {
                OrderNumber = orderNumber,
                MaterialNumber = materialNumber,
                LoadedMenusJson = JsonSerializer.Serialize(menuNames),
                CreatedBy = createdBy,
                CreatedTime = createdTime
            });

            lock (_stateLock)
            {
                // 若写入期间用户已登出，记录仍代表刚才已发生的切换，但不再污染新会话的去重状态
                if (requestedSessionVersion == _sessionVersion)
                {
                    _lastOrderNumber = orderNumber;
                    _lastMaterialNumber = materialNumber;
                }
            }

            return true;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <inheritdoc/>
    public void ResetSession()
    {
        lock (_stateLock)
        {
            _sessionVersion++;
            _lastOrderNumber = string.Empty;
            _lastMaterialNumber = string.Empty;
        }
    }

    private bool IsSameActiveOrder(string orderNumber, string materialNumber)
    {
        return string.Equals(orderNumber, _lastOrderNumber, StringComparison.OrdinalIgnoreCase)
            && string.Equals(materialNumber, _lastMaterialNumber, StringComparison.OrdinalIgnoreCase);
    }
}
