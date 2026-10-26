// =============================================================================
// 文件名：LoginViewModel.cs
// 功能描述：登录视图模型。
//           负责校验工号/密码、写入当前用户上下文、发布登录成功事件并导航至主视图。
// =============================================================================
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Events;
using IMDP.Application.Services;
using IMDP.Application.DTOs;
using IMDP.Application.Events;
using IMDP.Domain.Interfaces;
using IMDP.Domain.Users;
using IMDP.Infrastructure.Logging;

namespace IMDP.Presentation.ViewModels;

/// <summary>
/// 登录视图模型
/// <para>依赖 <see cref="IUserService"/> 校验密码、<see cref="ICurrentContext"/> 保存登录状态、
/// <see cref="IEventAggregator"/> 发布 <see cref="UserLoggedInEvent"/>，最后通过 <see cref="IRegionManager"/> 导航至主视图。</para>
/// </summary>
public partial class LoginViewModel : ObservableObject, INavigationAware
{
    private readonly IRegionManager _regionManager;
    private readonly IEventAggregator _eventAggregator;
    private readonly IUserService _userService;
    private readonly ICurrentContext _currentContext;

    /// <summary>NLog 日志（登录成功/失败/异常均记录工位与工号，不记录密码）</summary>
    private static readonly NLog.ILogger _logger = LogHelper.For<LoginViewModel>();

    /// <summary>工位编号（默认 "1"）</summary>
    [ObservableProperty]
    private string _station = "CSBSBCPCCJY-T1";

    /// <summary>登录工号（默认空，由用户输入）</summary>
    [ObservableProperty]
    private string _workNumber = "6666";

    /// <summary>登录密码（明文，仅用于传递给校验服务，不持久化）</summary>
    [ObservableProperty]
    private string _password = "1";

    /// <summary>是否离线登录（暂未启用，保留扩展点）</summary>
    [ObservableProperty]
    private bool _isOfflineLogin;

    /// <summary>登录命令（触发登录校验与导航）</summary>
    public IRelayCommand LoginCommand { get; }
    /// <summary>取消命令（退出应用程序）</summary>
    public IRelayCommand CancelCommand { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="regionManager">Prism 区域管理器（用于登录成功后导航至主视图）</param>
    /// <param name="eventAggregator">Prism 事件聚合器（发布登录成功事件）</param>
    /// <param name="userService">用户服务（校验密码与获取用户信息）</param>
    /// <param name="currentContext">当前用户上下文（保存登录工号）</param>
    private readonly IMessageService _messageService;
    public LoginViewModel(IRegionManager regionManager, IEventAggregator eventAggregator, IUserService userService, ICurrentContext currentContext,
        IMessageService messageService)
    {
        _messageService = messageService;
        _regionManager = regionManager;
        _eventAggregator = eventAggregator;
        _userService = userService;
        _currentContext = currentContext;

        LoginCommand = new RelayCommand(OnLogin);
        CancelCommand = new RelayCommand(OnCancel);
    }

    /// <summary>
    /// 登录处理
    /// <para>校验输入完整性 → 调用 <see cref="IUserService.LoginViaMesAsync"/> 由 MES 校验工号/密码/资源编码（不查本地库）→
    /// MES 通过后从本地表读取角色/姓名用于权限映射 → 写入当前用户上下文 → 记录登录日志（工位/工号/IP/登录类型/登录结果/登录时间，
    /// 成功与失败均记录，失败时弹窗提示错误信息）→ 发布 <see cref="UserLoggedInEvent"/> → 导航至主视图。</para>
    /// </summary>
    private async void OnLogin()
    {
        if (string.IsNullOrWhiteSpace(Station) || string.IsNullOrWhiteSpace(WorkNumber))
        {
            _messageService.ShowInfo("请输入工位和工号", "提示");
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            _messageService.ShowInfo("请输入密码", "提示");
            return;
        }

        try
        {
            // 1) MES 校验工号 + 密码 + 资源编码（工位号映射为 ResCode），不再查本地库做密码校验
            var mesResult = await _userService.LoginViaMesAsync(WorkNumber, Password, Station);
            if (!mesResult.Success)
            {
                // 失败也记录登录日志（含失败原因），随后弹窗提示
                await _userService.RecordLoginAsync(WorkNumber, Station, LoginTypeCodes.Mes, false, mesResult.ErrorInfo);
                _logger.Warn($"登录失败：工位={Station}，工号={WorkNumber}，原因={mesResult.ErrorInfo ?? "工号或密码错误"}");
                _messageService.ShowError(mesResult.ErrorInfo ?? "工号或密码错误");
                return;
            }

            // 2) MES 校验通过后，仍从本地 Users 表读取角色/姓名用于权限映射（不比对密码）
            var user = await _userService.GetUserAsync(WorkNumber);
            if (user == null)
            {
                // MES 已通过但本地无该用户记录：使用最小身份进入，角色取默认 Operator
                user = new UserDto { WorkNumber = WorkNumber, Name = WorkNumber, Role = UserRoleCodes.Operator };
            }

            _currentContext.SetUser(user.WorkNumber, user.Role);
            // 记录登录工位为 MES 资源编码（ResCode）：MES 上传 SaveProcedure 与
            // 登录校验同源，保证该工位对当前产品已通过工序卡控
            _currentContext.SetStation(Station);

            // 登录成功记入日志（工位 + 工号 + 姓名 + 角色）
            _logger.Info($"登录成功：工位={Station}，工号={user.WorkNumber}，姓名={user.Name}，角色={user.Role}");

            // 3) 记录登录日志到 LoginConfig 表（工位/工号/IP/登录类型/登录结果/登录时间；写入失败不阻断登录）
            //    保存返回的记录 Id，供正常退出登录时回填登出时间
            var loginRecordId = await _userService.RecordLoginAsync(user.WorkNumber, Station, LoginTypeCodes.Mes, true, null);
            if (loginRecordId > 0)
                _currentContext.SetCurrentLoginRecordId(loginRecordId);

            // 发布登录成功事件，供 MainViewModel 等订阅者更新角色/工位信息
            _eventAggregator.GetEvent<UserLoggedInEvent>().Publish(new UserLoggedInPayload
            {
                WorkNumber = user.WorkNumber,
                UserName = user.Name,
                Role = user.Role,
                Station = Station,
                LoginTime = DateTime.Now
            });

            // 导航至主视图
            _regionManager.RequestNavigate("MainContentRegion", "MainView");
        }
        catch (Exception ex)
        {
            // 异常也按一次失败登录记录（原因=异常信息），随后弹窗提示
            await _userService.RecordLoginAsync(WorkNumber, Station, LoginTypeCodes.Mes, false, ex.Message);
            _logger.Error(ex, $"登录异常：工位={Station}，工号={WorkNumber}");
            _messageService.ShowError($"登录失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 取消处理：直接退出应用程序
    /// </summary>
    private void OnCancel()
    {
        System.Windows.Application.Current.Shutdown();
    }

    /// <inheritdoc/>
    public bool IsNavigationTarget(NavigationContext navigationContext) => true;
    /// <inheritdoc/>
    public void OnNavigatedFrom(NavigationContext navigationContext) { }
    /// <inheritdoc/>
    public void OnNavigatedTo(NavigationContext navigationContext) { }
}
