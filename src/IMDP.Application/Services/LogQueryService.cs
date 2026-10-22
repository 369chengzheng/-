// =============================================================================
// 文件名：LogQueryService.cs
// 功能描述：日志查询服务实现，封装登录日志、升级日志、工单切换日志的分页查询。
//           依赖 ILoginConfigRepository/IAppUpdateRecordRepository/IOrderSwitchRepository 完成数据库查询，
//           并将实体映射为对应 DTO 返回给上层 ViewModel。
// =============================================================================
using IMDP.Application.DTOs;
using IMDP.Domain.Interfaces;

namespace IMDP.Application.Services;

/// <summary>
/// 日志查询服务实现
/// <para>实现 <see cref="ILogQueryService"/>，统一处理登录/升级/工单切换三类日志的查询与 DTO 映射。</para>
/// <para>依赖三个仓储：<see cref="ILoginConfigRepository"/>、<see cref="IAppUpdateRecordRepository"/>、<see cref="IOrderSwitchRepository"/>。</para>
/// </summary>
public class LogQueryService : ILogQueryService
{
    private readonly ILoginConfigRepository _loginConfigRepository;
    private readonly IAppUpdateRecordRepository _appUpdateRecordRepository;
    private readonly IOrderSwitchRepository _orderSwitchRepository;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="loginConfigRepository">登录日志仓储（LoginConfig 表）</param>
    /// <param name="appUpdateRecordRepository">应用升级记录仓储</param>
    /// <param name="orderSwitchRepository">工单切换记录仓储</param>
    public LogQueryService(ILoginConfigRepository loginConfigRepository, IAppUpdateRecordRepository appUpdateRecordRepository, IOrderSwitchRepository orderSwitchRepository)
    {
        _loginConfigRepository = loginConfigRepository;
        _appUpdateRecordRepository = appUpdateRecordRepository;
        _orderSwitchRepository = orderSwitchRepository;
    }

    /// <inheritdoc/>
    public async Task<PagedResultDto<LoginRecordDto>> GetLoginRecordsAsync(string workNumber, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize)
    {
        var result = await _loginConfigRepository.GetPagedAsync(workNumber, startTime, endTime, pageIndex, pageSize);
        return new PagedResultDto<LoginRecordDto>
        {
            Items = result.Items.Select(MapToDto).ToList(),
            TotalCount = result.TotalCount,
            PageIndex = result.PageIndex,
            PageSize = result.PageSize
        };
    }

    /// <inheritdoc/>
    public async Task<PagedResultDto<AppUpdateRecordDto>> GetUpdateRecordsAsync(string versionKeyword, string releaseNotesKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize)
    {
        var result = await _appUpdateRecordRepository.GetPagedAsync(versionKeyword, releaseNotesKeyword, startTime, endTime, pageIndex, pageSize);
        return new PagedResultDto<AppUpdateRecordDto>
        {
            Items = result.Items.Select(MapToDto).ToList(),
            TotalCount = result.TotalCount,
            PageIndex = result.PageIndex,
            PageSize = result.PageSize
        };
    }

    /// <inheritdoc/>
    public async Task<PagedResultDto<OrderSwitchRecordDto>> GetOrderSwitchRecordsAsync(string orderNumberKeyword, string operatorKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize, bool ascending = false)
    {
        var result = await _orderSwitchRepository.GetPagedAsync(orderNumberKeyword, operatorKeyword, startTime, endTime, pageIndex, pageSize, ascending);
        return new PagedResultDto<OrderSwitchRecordDto>
        {
            Items = result.Items.Select(MapToDto).ToList(),
            TotalCount = result.TotalCount,
            PageIndex = result.PageIndex,
            PageSize = result.PageSize
        };
    }

    /// <summary>将 LoginConfig 实体映射为 LoginRecordDto</summary>
    private static LoginRecordDto MapToDto(IMDP.Domain.Entities.LoginConfig l) => new()
    {
        Id = l.Id,
        Station = l.Station,
        WorkNumber = l.WorkNumber,
        IpAddress = l.IpAddress,
        LoginType = l.LoginType,
        LoginResult = l.LoginResult,
        ErrorInfo = l.ErrorInfo,
        SoftVer = l.SoftVer,
        LoginTime = l.LoginTime,
        LogoutTime = l.LogoutTime,
        ComputerName = l.ComputerName,
        UserName = l.UserName,
        AppName = l.AppName
    };

    /// <summary>将 AppUpdateRecord 实体映射为 AppUpdateRecordDto</summary>
    private static AppUpdateRecordDto MapToDto(IMDP.Domain.Entities.AppUpdateRecord r) => new()
    {
        Id = r.Id,
        CurrentVersion = r.CurrentVersion,
        TargetVersion = r.TargetVersion,
        ReleaseNotes = r.ReleaseNotes,
        UpdateStatus = r.UpdateStatus,
        UpdateTime = r.UpdateTime,
        CompletedTime = r.CompletedTime,
        ErrorMessage = r.ErrorMessage,
        ComputerName = r.ComputerName,
        UserName = r.UserName,
        AppName = r.AppName
    };

    /// <summary>将 OrderSwitch 实体映射为 OrderSwitchRecordDto</summary>
    private static OrderSwitchRecordDto MapToDto(IMDP.Domain.Entities.OrderSwitch o) => new()
    {
        Id = o.Id,
        OrderNumber = o.OrderNumber,
        MaterialNumber = o.MaterialNumber,
        LoadedMenusJson = o.LoadedMenusJson,
        CreatedBy = o.CreatedBy,
        CreatedTime = o.CreatedTime,
        ComputerName = o.ComputerName,
        UserName = o.UserName,
        AppName = o.AppName
    };
}
