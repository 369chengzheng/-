// =============================================================================
// 文件名：MainWindowViewModel.cs
// 功能描述：主窗口视图模型。
//           负责顶部用户信息栏、菜单弹窗、登录/登出状态管理，
//           以及将各类导航请求转发至主内容区域（含产品插件导航）。
// =============================================================================
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Common;
using Prism.Ioc;
using Prism.Events;
using IMDP.Application.DTOs;
using IMDP.Application.Events;
using IMDP.Application.Services;
using IMDP.Domain.Interfaces;
using IMDP.Domain.Products;
using IMDP.Presentation.Views;
using IMDP.Domain.Users;
using NLog;

using IMDP.Infrastructure.Logging;
namespace IMDP.Presentation.ViewModels;

/// <summary>
/// 主窗口视图模型
/// <para>订阅 <see cref="UserLoggedInEvent"/>/<see cref="UserLoggedOutEvent"/> 等事件维护用户态；
/// 提供菜单弹窗控制、各功能模块导航命令，以及登出/返回主页等命令。</para>
/// <para>产品插件菜单项的导航通过 <see cref="IProductPluginNavigationService"/> 解析视图名与计划类型，
/// 再经 <see cref="NavigateToModuleEvent"/> 由宿主视图统一执行区域导航。</para>
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IRegionManager _regionManager;
    private readonly IEventAggregator _eventAggregator;
    private readonly IContainerProvider _containerProvider;
    private readonly IProductPluginNavigationService _pluginNavigationService;
    private readonly ICurrentContext _currentContext;
    private readonly IUpdateService _updateService;
    private static readonly ILogger _logger = LogHelper.For<MainWindowViewModel>();

    private readonly IOrderSwitchRecordService _orderSwitchRecordService;
    private readonly IUserService _userService;
    /// <summary>当前物料对应的原始菜单，角色变化时以此为数据源重新过滤。</summary>
    private List<ProductMenuItemDto> _allMenuItems = new();
    /// <summary>当前应用版本号（从 appsettings.json 配置/更新服务动态获取）</summary>
    [ObservableProperty]
    private string _appVersion = string.Empty;

    /// <summary>当前工位编号</summary>
    [ObservableProperty]
    private string _station = string.Empty;

    /// <summary>当前登录用户工号</summary>
    [ObservableProperty]
    private string _workNumber = string.Empty;

    /// <summary>当前登录用户姓名</summary>
    [ObservableProperty]
    private string _userName = string.Empty;

    /// <summary>登录时间（格式化字符串）</summary>
    [ObservableProperty]
    private string _loginTime = string.Empty;

    /// <summary>当前登录用户角色</summary>
    [ObservableProperty]
    private string _role = string.Empty;

    /// <summary>
    /// 当前物料号对应的产品功能模块菜单项
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ProductMenuItemDto> _menuItems = new();

    /// <summary>
    /// 菜单弹窗是否打开
    /// </summary>
    [ObservableProperty]
    private bool _isMenuPopupOpen;

    /// <summary>
    /// 当前是否为系统管理员
    /// </summary>
    [ObservableProperty]
    private bool _isSystemAdmin;

    /// <summary>
    /// 当前是否为管理员或系统管理员（方案配置可见）
    /// </summary>
    [ObservableProperty]
    private bool _isAdminOrAbove;

    /// <summary>
    /// 当前是否为配置员及以上（方案查询可见）
    /// </summary>
    [ObservableProperty]
    private bool _isConfiguratorOrAbove;

    /// <summary>
    /// 当前是否已登录
    /// </summary>
    [ObservableProperty]
    private bool _isLoggedIn;

    /// <summary>当前软件物料号（由主界面查询时发布的事件更新，用于菜单导航时传递给目标视图）</summary>
    private string _currentMaterialNumber = string.Empty;

    /// <summary>
    /// 当前已经生效的制令单号，不从首页尚未查询的输入框取值
    /// </summary>
    private string _currentOrderNumber = string.Empty;

    /// <summary>
    /// 当前已生效制令单返回的MES产品名称，手动物料查询时清空
    /// </summary>
    private string _currentProductName = string.Empty;

    /// <summary>菜单弹窗开关命令</summary>
    public IRelayCommand MenuCommand { get; }
    /// <summary>返回主页命令（清空主区域并导航至主视图）</summary>
    public IRelayCommand GoHomeCommand { get; }
    /// <summary>菜单项点击命令（解析视图名后通过事件导航）</summary>
    public IRelayCommand<ProductMenuItemDto?> MenuItemClickCommand { get; }
    /// <summary>
    /// 通用导航命令（参数为目标视图名，如 "UserManageView"）。
    /// <para>合并原 10 个 NavigateTo{Xxx}Command，由 XAML 通过 CommandParameter 指定视图名。</para>
    /// </summary>
    public IRelayCommand<string> NavigateCommand { get; }
    /// <summary>登出命令（清除上下文并导航至登录视图）</summary>
    public IRelayCommand LogoutCommand { get; }

    /// <summary>检查更新命令（弹岀升级更新窗口 4.2 UpdateWindow）</summary>
    public IRelayCommand CheckUpdateCommand { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="regionManager">Prism 区域管理器</param>
    /// <param name="eventAggregator">Prism 事件聚合器</param>
    /// <param name="containerProvider">Prism 容器提供者（用于解析对话框视图）</param>
    /// <param name="pluginNavigationService">产品插件导航服务</param>
    /// <param name="currentContext">当前用户上下文</param>
    /// <param name="updateService">应用更新服务（用于读取当前版本号配置）</param>
    private readonly IMessageService _messageService;
    public MainWindowViewModel(IRegionManager regionManager, IEventAggregator eventAggregator,
        IContainerProvider containerProvider, IProductPluginNavigationService pluginNavigationService,
        ICurrentContext currentContext, IUpdateService updateService,
        IOrderSwitchRecordService orderSwitchRecordService,
        IMessageService messageService, IUserService userService)
    {
        _messageService = messageService;
        _regionManager = regionManager;
        _eventAggregator = eventAggregator;
        _containerProvider = containerProvider;
        _pluginNavigationService = pluginNavigationService;
        _currentContext = currentContext;
        _updateService = updateService;
        _orderSwitchRecordService = orderSwitchRecordService;
        _userService = userService;

        // 从配置/应用更新服务初始化当前版本号
        AppVersion = _updateService.GetCurrentVersion();
        
        MenuCommand = new RelayCommand(OnMenu);
        GoHomeCommand = new RelayCommand(OnGoHome);
        MenuItemClickCommand = new RelayCommand<ProductMenuItemDto?>(OnMenuItemClick);
        // 通用导航命令：合并原 10 个 NavigateTo{Xxx}Command，由 XAML 通过 CommandParameter 指定视图名
        NavigateCommand = new RelayCommand<string>(viewName => NavigateTo(viewName!));
        LogoutCommand = new RelayCommand(OnLogout);
        // 检查更新命令：弹岀升级更新窗口（4.2）
        CheckUpdateCommand = new RelayCommand(OnCheckUpdate);

        // ===== 订阅全局事件 =====
        _eventAggregator.GetEvent<UserLoggedInEvent>().Subscribe(OnUserLoggedIn, ThreadOption.UIThread);
        _eventAggregator.GetEvent<UserLoggedOutEvent>().Subscribe(OnUserLoggedOut, ThreadOption.UIThread);
        _eventAggregator.GetEvent<OpenSettingsDialogEvent>().Subscribe(OnOpenSettings, ThreadOption.UIThread);
        _eventAggregator.GetEvent<NavigateToModuleEvent>().Subscribe(OnNavigateToModule, ThreadOption.UIThread);
        _eventAggregator.GetEvent<HomeMenuResolvedEvent>().Subscribe(OnHomeMenuResolved, ThreadOption.UIThread);
        _eventAggregator.GetEvent<GoHomeEvent>().Subscribe(OnGoHome, ThreadOption.UIThread);
        _eventAggregator.GetEvent<EndWorkOrderEvent>().Subscribe(OnEndWorkOrder, ThreadOption.UIThread);
        _eventAggregator.GetEvent<UserRoleChangedEvent>().Subscribe(OnUserRoleChanged, ThreadOption.UIThread);
    }

    /// <summary>
    /// 接收首页一次查询的完整解析结果，更新菜单并按需写入工单切换记录
    /// </summary>
    /// <param name="payload">制令单号、物料号、原始菜单和发起人</param>
    private void OnHomeMenuResolved(HomeMenuResolvedPayload payload)
    {
        try
        {
            // 忽略登出后才到达的结果，或其他登录会话遗留的结果
            if (!IsLoggedIn
                || !string.Equals(payload.OperatorWorkNumber, WorkNumber, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var candidateRawMenus = payload.RawMenuItems?.DistinctBy(item => item.ModuleType).ToList()
                ?? new List<ProductMenuItemDto>();
            var candidateVisibleMenus = FilterMenuItems(candidateRawMenus);

            // 候选结果未完整解析成功时，不替换已经生效的工单、物料和菜单
            if (string.IsNullOrWhiteSpace(payload.MaterialNumber) || candidateVisibleMenus.Count == 0)
            {
                _logger.Warn($"主页查询结果未生效，保留当前工单和菜单：{payload.OrderNumber} -> {payload.MaterialNumber}");
                _messageService.ShowInfo("该物料在当前角色和工位下没有可用菜单，当前工单和菜单保持不变。", "提示");
                return;
            }

            _allMenuItems = candidateRawMenus;
            MenuItems = new ObservableCollection<ProductMenuItemDto>(candidateVisibleMenus);
            _currentMaterialNumber = payload.MaterialNumber.Trim();
            _currentOrderNumber = payload.OrderNumber.Trim();
            _currentProductName = string.IsNullOrEmpty(_currentOrderNumber) ? string.Empty : payload.ProductName.Trim();

            // 手动物料查询没有制令单号，不属于工单切换；同时结束之前的活动工单状态
            if (string.IsNullOrWhiteSpace(payload.OrderNumber))
            {
                _orderSwitchRecordService.ResetSession();
                return;
            }

            _ = RecordOrderSwitchAsync(payload, candidateVisibleMenus);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "处理主页菜单解析结果失败");
        }
    }

    /// <summary>后台写入工单切换记录；失败只记日志，不影响已经显示的主页菜单</summary>
    private async Task RecordOrderSwitchAsync(HomeMenuResolvedPayload payload, IReadOnlyList<ProductMenuItemDto> visibleMenus)
    {
        try
        {
            var inserted = await _orderSwitchRecordService.RecordIfChangedAsync(
                payload.OrderNumber,
                payload.MaterialNumber,
                visibleMenus,
                payload.OperatorWorkNumber,
                payload.ResolvedTime);

            if (inserted)
            {
                _logger.Info($"已记录工单切换：{payload.OrderNumber} -> {payload.MaterialNumber}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"工单切换记录写入失败：{payload.OrderNumber} -> {payload.MaterialNumber}");
        }
    }

    /// <summary>
    /// 菜单弹窗开关：切换 <see cref="IsMenuPopupOpen"/>
    /// </summary>
    private void OnMenu()
    {
        IsMenuPopupOpen = !IsMenuPopupOpen;
    }

    /// <summary>
    /// 按当前角色和登录工位过滤原始菜单
    /// </summary>
    private void ApplyMenuFilter()
    {
        MenuItems = new ObservableCollection<ProductMenuItemDto>(FilterMenuItems(_allMenuItems));
    }

    /// <summary>根据当前角色和工位计算候选菜单，不直接修改已生效的页面状态</summary>
    private List<ProductMenuItemDto> FilterMenuItems(IEnumerable<ProductMenuItemDto> source)
    {
        var modules = source.DistinctBy(item => item.ModuleType).ToList();

        //如果是作业员，过滤掉配置方案
        if (Role == UserRoleCodes.Operator)
            modules = modules.Where(m => m.ModuleType is not (FunctionModuleType.WholeSetConfig or FunctionModuleType.WholeGetConfig or FunctionModuleType.BoardConfig)).ToList();

        //设置工序不加载整机检验，检验工序不加载整机设置；未识别或同时包含两种前缀时保留全部菜单
        var isSettingStation = Station.Contains("SZ-", StringComparison.OrdinalIgnoreCase);
        var isInspectionStation = Station.Contains("JY-", StringComparison.OrdinalIgnoreCase);
        if (isSettingStation ^ isInspectionStation)
        {
            modules = isSettingStation
                ? modules.Where(m => m.ModuleType is not (FunctionModuleType.WholeGetConfig or FunctionModuleType.WholeGet)).ToList()
                : modules.Where(m => m.ModuleType is not (FunctionModuleType.WholeSetConfig or FunctionModuleType.WholeSet)).ToList();
        }

        return modules;
    }

    /// <summary>
    /// 菜单项点击处理
    /// <para>通过插件导航服务解析视图名与计划类型，并以 <see cref="NavigateToModuleEvent"/> 发布导航请求。</para>
    /// <para>同时携带当前软件物料号，供目标视图在面包屑中显示。</para>
    /// </summary>
    /// <param name="module">被点击的功能模块类型</param>
    private void OnMenuItemClick(ProductMenuItemDto? module)
    {
        if (module == null) return;
        IsMenuPopupOpen = false;

        var viewName = _pluginNavigationService.GetViewName(module.ModuleType, module.ProductKey);
        if (string.IsNullOrEmpty(viewName))
        {
            // 路由未解析：插件未注册、未声明路由契约，或路由表缺少该功能项——记日志便于排查"点菜单没反应"
            _logger.Warn($"菜单导航未解析到目标视图：功能={module.ModuleType}，插件={module.ProductKey}");
            return;
        }

        var planType = _pluginNavigationService.GetPlanType(module.ModuleType);

        var payload = new NavigateToModulePayload
        {
            ViewName = viewName,
            PlanType = planType,
            MaterialNumber = _currentMaterialNumber,
            ProductName = _currentProductName,
            OrderNumber = _currentOrderNumber,
            ResourceCode = Station,
            OperatorWorkNumber = WorkNumber
        };
        _eventAggregator.GetEvent<NavigateToModuleEvent>().Publish(payload);
    }

    /// <summary>
    /// 导航至指定视图（关闭菜单弹窗）
    /// </summary>
    /// <param name="viewName">目标视图名</param>
    private void NavigateTo(string viewName)
    {
        IsMenuPopupOpen = false;
        _regionManager.RequestNavigate("MainContentRegion", viewName);
    }

    /// <summary>
    /// 打开设置对话框
    /// <para>从容器解析 <see cref="SettingsDialogView"/> 并以主窗口为 Owner 模态显示。</para>
    /// </summary>
    private void OnOpenSettings()
    {
        var dialog = _containerProvider.Resolve<SettingsDialogView>();
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        dialog.ShowDialog();
    }

    /// <summary>
    /// 检查更新
    /// <para>从容器解析升级更新窗口 <see cref="UpdateWindow"/>（4.2）并以主窗口为 Owner 模态显示。</para>
    /// <para>窗口构造时会自动调用 IUpdateService.CheckUpdateAsync 检测服务器最新版本并填充界面。</para>
    /// </summary>
    private void OnCheckUpdate()
    {
        IsMenuPopupOpen = false;
        var window = _containerProvider.Resolve<UpdateWindow>();
        window.Owner = System.Windows.Application.Current.MainWindow;
        if (window.DataContext is UpdateViewModel vm)
        {
            vm.IsForceUpdate = false;
        }
        window.ShowDialog();
    }

    /// <summary>
    /// 模块导航事件回调
    /// <para>携带 PlanType 与 MaterialNumber 导航参数跳转到目标视图。</para>
    /// </summary>
    /// <param name="payload">导航负载（视图名 + 计划类型 + 软件物料号）</param>
    private void OnNavigateToModule(NavigateToModulePayload payload)
    {
        var parameters = new NavigationParameters();
        if (!string.IsNullOrEmpty(payload.PlanType))
        {
            parameters.Add("PlanType", payload.PlanType);
        }
        if (!string.IsNullOrEmpty(payload.MaterialNumber))
        {
            parameters.Add("MaterialNumber", payload.MaterialNumber);
        }
        parameters.Add("OrderNumber", payload.OrderNumber);
        parameters.Add("ProductName", payload.ProductName);
        parameters.Add("ResourceCode", payload.ResourceCode);
        parameters.Add("OperatorWorkNumber", payload.OperatorWorkNumber);
        _regionManager.RequestNavigate("MainContentRegion", payload.ViewName, parameters);
    }

    /// <summary>
    /// 用户登录成功回调：刷新用户信息栏字段
    /// </summary>
    /// <param name="payload">登录事件负载</param>
    private void OnUserLoggedIn(UserLoggedInPayload payload)
    {
        // 重置工单切换记录的会话去重状态，避免沿用上一次登录的工单状态
        _orderSwitchRecordService.ResetSession();

        // 清空上一登录会话缓存的原始功能菜单
        _allMenuItems.Clear();

        // 清空界面当前显示的功能菜单
        MenuItems.Clear();

        // 清空当前软件物料号，避免后续导航携带上一会话的物料号
        _currentMaterialNumber = string.Empty;
        _currentOrderNumber = string.Empty;
        _currentProductName = string.Empty;

        Station = payload.Station;
        WorkNumber = payload.WorkNumber;
        UserName = payload.UserName;
        Role = payload.Role;
        LoginTime = payload.LoginTime.ToString("yyyy-MM-dd HH:mm:ss");
        IsSystemAdmin = payload.Role == UserRoleCodes.SystemAdmin;
        IsAdminOrAbove = payload.Role is UserRoleCodes.SystemAdmin or UserRoleCodes.Admin;
        IsConfiguratorOrAbove = payload.Role is UserRoleCodes.SystemAdmin or UserRoleCodes.Admin or UserRoleCodes.Configurator;
        IsLoggedIn = true;
    }

    /// <summary>
    /// 用户登出回调：清空所有用户信息栏字段
    /// </summary>
    private void OnUserLoggedOut()
    {
        _orderSwitchRecordService.ResetSession();
        _allMenuItems.Clear();
        _currentMaterialNumber = string.Empty;
        _currentOrderNumber = string.Empty;
        _currentProductName = string.Empty;

        Station = string.Empty;
        WorkNumber = string.Empty;
        UserName = string.Empty;
        Role = string.Empty;
        LoginTime = string.Empty;
        MenuItems.Clear();
        IsSystemAdmin = false;
        IsAdminOrAbove = false;
        IsConfiguratorOrAbove = false;
        IsLoggedIn = false;
    }

    /// <summary>
    /// 登出处理：回填登出时间 → 清除当前用户上下文 → 发布登出事件 → 导航至登录视图
    /// </summary>
    private async void OnLogout()
    {
        // 正常退出登录：回填本次登录写入 LoginConfig 表的登出时间（记录缺失则忽略）
        if (_currentContext.CurrentLoginRecordId.HasValue)
        {
            try
            {
                await _userService.RecordLogoutAsync(_currentContext.CurrentLoginRecordId.Value);
            }
            catch
            {
                // 登出时间回填失败不应阻断登出流程，静默忽略
            }
        }

        _currentContext.ClearUser();
        _eventAggregator.GetEvent<UserLoggedOutEvent>().Publish();
        _regionManager.RequestNavigate("MainContentRegion", "LoginView");
    }

    /// <summary>
    /// 返回主页处理
    /// <para>关闭菜单弹窗，通知当前页面离开，清空主内容区域所有视图，再导航至主视图。</para>
    /// </summary>
    private void OnGoHome()
    {
        IsMenuPopupOpen = false;
        // 移除主内容区域中所有现有视图，确保回到主视图时为干净状态
        var mainContentRegion = _regionManager.Regions["MainContentRegion"];
        // 直接移除视图不会触发导航回调，先通知当前视图及其视图模型离开
        var navigationContext = new NavigationContext(mainContentRegion.NavigationService, new Uri("MainView", UriKind.Relative));
        foreach (var view in mainContentRegion.ActiveViews.ToList())
        {
            MvvmHelpers.ViewAndViewModelAction<INavigationAware>(view, page => page.OnNavigatedFrom(navigationContext));
        }
        foreach (var view in mainContentRegion.Views.ToList())
        {
            mainContentRegion.Remove(view);
        }
        _regionManager.RequestNavigate("MainContentRegion", "MainView");
    }

    /// <summary>
    /// 结束已生效工单并重新进入空首页，后续必须重新查询物料或制令单
    /// </summary>
    private void OnEndWorkOrder()
    {
        _orderSwitchRecordService.ResetSession();
        _currentOrderNumber = string.Empty;
        _currentMaterialNumber = string.Empty;
        _currentProductName = string.Empty;
        _allMenuItems.Clear();
        MenuItems.Clear();
        OnGoHome();
    }

    /// <summary>
    /// 用户角色变更回调：若变更的是当前登录用户，则刷新主窗口的角色、菜单权限状态
    /// </summary>
    /// <param name="payload">包含工号与新角色的变更信息</param>
    private void OnUserRoleChanged(UserRoleChangedPayload payload)
    {
        if (!string.Equals(payload.WorkNumber, WorkNumber, StringComparison.OrdinalIgnoreCase))
            return;

        Role = payload.New_Role;
        IsSystemAdmin = payload.New_Role == UserRoleCodes.SystemAdmin;
        IsAdminOrAbove = payload.New_Role is UserRoleCodes.SystemAdmin or UserRoleCodes.Admin;
        IsConfiguratorOrAbove = payload.New_Role is UserRoleCodes.SystemAdmin or UserRoleCodes.Admin or UserRoleCodes.Configurator;
        ApplyMenuFilter();
    }

}
