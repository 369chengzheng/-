// =============================================================================
// 文件名：MainViewModel.cs
// 功能描述：主视图模型。
//           负责接收登录信息（角色/工位）、根据软件物料号查询可用功能模块，
//           并通过事件聚合器将菜单刷新结果广播给主视图的菜单区域。
// =============================================================================
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Events;
using IMDP.Application.Services;
using IMDP.Application.Events;
using IMDP.Domain.Products;
using NLog;
using IMDP.Domain.Users;
using IMDP.Domain.Interfaces;

using IMDP.Infrastructure.Logging;
namespace IMDP.Presentation.ViewModels;

/// <summary>
/// 主视图模型
/// <para>订阅 <see cref="UserLoggedInEvent"/> 接收登录用户角色与权限；
/// 提供物料号查询命令，调用 <see cref="IProductPluginRegistry"/> 获取当前产品可用功能模块，
/// 并通过 <see cref="HomeMenuResolvedEvent"/> 原子传递工单、物料和菜单解析结果</para>
/// </summary>
public partial class MainViewModel : ObservableObject, INavigationAware
{
    private static readonly ILogger _logger = LogHelper.For<MainViewModel>();
    private readonly IRegionManager _regionManager;
    private readonly IEventAggregator _eventAggregator;
    private readonly IProductPluginRegistry _pluginRegistry;
    private readonly IMesService _mesService;
    private readonly ICurrentContext _currentContext;

    /// <summary>当前制令单号</summary>
    [ObservableProperty]
    private string _orderNumber = string.Empty;

    /// <summary>软件物料号（用于查询该物料对应产品可用的功能模块）</summary>
    [ObservableProperty]
    private string _materialNumber = string.Empty;

    /// <summary>最近一次成功制令单查询返回的完整产品信息</summary>
    [ObservableProperty]
    private MesMoProductInfo? _productInfo;

    /// <summary>是否已有可展示的制令单产品信息</summary>
    public bool HasProductInfo => ProductInfo != null;

    /// <summary>软件清单首项的软件物料号（当前菜单解析所使用的物料号）</summary>
    public string PrimarySoftwareMaterialNumber =>
        ProductInfo?.SoftwareList.FirstOrDefault()?.SoftItemCode ?? "未返回";

    /// <summary>软件清单首项的主控板物料编码</summary>
    public string PrimaryMainBoardCode =>
        ProductInfo?.SoftwareList.FirstOrDefault()?.MainBoardCode ?? "未返回";

    /// <summary>是否显示物料号输入框（仅管理员及以上角色可见）</summary>
    [ObservableProperty]
    private bool _showMaterialNumber;

    /// <summary>当前登录用户角色（由 <see cref="UserLoggedInEvent"/> 推送）</summary>
    [ObservableProperty]
    private string _role = string.Empty;

    /// <summary>是否正在查询（用于禁用查询按钮与显示加载状态）</summary>
    [ObservableProperty]
    private bool _isQuerying;

    /// <summary>是否未在查询中（<see cref="IsQuerying"/> 的反向值，供绑定禁用按钮）</summary>
    public bool IsNotQuerying => !IsQuerying;

    // IsQuerying 变化时同步通知 IsNotQuerying 刷新绑定
    partial void OnIsQueryingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotQuerying));
    }

    // 完整产品信息替换后，同步刷新信息面板及首项软件/主板物料显示
    partial void OnProductInfoChanged(MesMoProductInfo? value)
    {
        OnPropertyChanged(nameof(HasProductInfo));
        OnPropertyChanged(nameof(PrimarySoftwareMaterialNumber));
        OnPropertyChanged(nameof(PrimaryMainBoardCode));
    }

    /// <summary>查询命令（根据物料号拉取功能模块并广播菜单刷新事件）</summary>
    public IAsyncRelayCommand QueryCommand { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="regionManager">Prism 区域管理器</param>
    /// <param name="eventAggregator">Prism 事件聚合器（订阅登录事件、发布菜单刷新事件）</param>
    /// <param name="pluginRegistry">产品插件注册表（查询物料对应的功能模块）</param>
    private readonly IMessageService _messageService;
    public MainViewModel(IRegionManager regionManager, IEventAggregator eventAggregator, IProductPluginRegistry pluginRegistry, IMesService mesService, ICurrentContext currentContext,
        IMessageService messageService)
    {
        _messageService = messageService;
        _regionManager = regionManager;
        _eventAggregator = eventAggregator;
        _pluginRegistry = pluginRegistry;
        _mesService = mesService;
        _currentContext = currentContext;

        //直接从上下文读取角色（先发布事件了，MainViewModel 在登录后创建才调用下面，OnUserLoggedIn留着后续事件）
        Role = _currentContext.Role;
        ShowMaterialNumber = _currentContext.Role is UserRoleCodes.SystemAdmin or UserRoleCodes.Admin or UserRoleCodes.Configurator;

        QueryCommand = new AsyncRelayCommand(OnQueryAsync);

        // 订阅登录成功事件，更新当前角色与物料号输入框可见性
        _eventAggregator.GetEvent<UserLoggedInEvent>().Subscribe(OnUserLoggedIn, ThreadOption.UIThread);
    }

    /// <summary>
    /// 用户登录成功回调
    /// <para>更新角色，并为配置员及以上角色显示物料号输入框。</para>
    /// </summary>
    /// <param name="payload">登录事件负载（包含角色等信息）</param>
    private void OnUserLoggedIn(UserLoggedInPayload payload)
    {
        Role = payload.Role;
        ShowMaterialNumber = payload.Role is UserRoleCodes.SystemAdmin or UserRoleCodes.Admin or UserRoleCodes.Configurator;
        ProductInfo = null;
    }

    /// <summary>
    /// 执行物料号查询
    /// <para>校验输入 → 通过 MES 解析制令单对应物料 → 查询功能模块 → 发布完整菜单解析结果</para>
    /// <para>查询失败时不发布结果，保留当前已经生效的工单和菜单</para>
    /// </summary>
    private async Task OnQueryAsync()
    {
        if (IsQuerying)
            return;

        var isManualMaterialQuery = !string.IsNullOrWhiteSpace(MaterialNumber);
        var requestedOrderNumber = isManualMaterialQuery ? string.Empty : OrderNumber.Trim();

        if (!isManualMaterialQuery && string.IsNullOrWhiteSpace(requestedOrderNumber))
        {
            _messageService.ShowInfo("请输入软件物料号或制令单号", "提示");
            return;
        }

        IsQuerying = true;
        try
        {
            var resolvedMaterialNumber = MaterialNumber.Trim();
            MesMoProductInfo? queriedProductInfo = null;

            // 作业员以及未手动物料查询的高权限用户，通过制令单号调用 MES 获取软件物料号
            if (!isManualMaterialQuery)
            {
                _logger.Info($"通过制令单号 {requestedOrderNumber} 从 MES 获取完整产品信息");
                var mesResult = await _mesService.GetProductInfoByMoAsync(requestedOrderNumber);
                queriedProductInfo = mesResult.ProductInfo;
                if (!mesResult.Success || queriedProductInfo == null)
                {
                    var error = string.IsNullOrWhiteSpace(mesResult.ErrorInfo)
                        ? "未找到该制令单号对应的产品信息"
                        : mesResult.ErrorInfo.Trim();
                    _messageService.ShowWarning($"{error}，当前工单和菜单保持不变", "警告");
                    return;
                }

                var mesMaterial = queriedProductInfo.SoftwareList.FirstOrDefault()?.SoftItemCode;
                if (string.IsNullOrWhiteSpace(mesMaterial))
                {
                    _messageService.ShowWarning("MES 未返回软件物料号，当前工单和菜单保持不变", "警告");
                    return;
                }

                resolvedMaterialNumber = mesMaterial.Trim();
                MaterialNumber = resolvedMaterialNumber;
            }

            _logger.Info($"开始查询物料号: {resolvedMaterialNumber}");
            // 后台线程调用插件注册表，避免阻塞 UI
            var menuItems = await Task.Run(() => _pluginRegistry.GetMenuEntriesForMaterialAsync(resolvedMaterialNumber));
            var menuSnapshot = menuItems.ToList();
            var moduleSnapshot = menuSnapshot.Select(item => item.ModuleType).ToList();

            _eventAggregator.GetEvent<HomeMenuResolvedEvent>().Publish(new HomeMenuResolvedPayload
            {
                OrderNumber = requestedOrderNumber,
                MaterialNumber = resolvedMaterialNumber,
                ProductName = queriedProductInfo?.ProductName?.Trim() ?? string.Empty,
                RawModules = moduleSnapshot,
                RawMenuItems = menuSnapshot,
                OperatorWorkNumber = _currentContext.UserName,
                ResolvedTime = DateTime.Now
            });

            // 制令单查询展示 MES 完整返回；手动物料查询没有对应 MES 产品信息，清除旧内容避免误关联
            ProductInfo = isManualMaterialQuery ? null : queriedProductInfo;

            // 查询完成后清空物料输入；手动物料查询同时清除可能残留的旧工单号，避免视觉误关联
            MaterialNumber = string.Empty;
            if (isManualMaterialQuery)
                OrderNumber = string.Empty;

            _logger.Info($"物料号查询完成，共 {moduleSnapshot.Count} 个菜单项");
        }
        catch (Exception ex)
        {
            if (!isManualMaterialQuery && string.IsNullOrWhiteSpace(MaterialNumber))
            {
                _logger.Error(ex, $"MES 查询失败，制令单号: {requestedOrderNumber}");
                _messageService.ShowError("MES 查询失败，当前工单和菜单保持不变，请稍后重试。", "错误");
            }
            else
            {
                _logger.Error(ex, $"查询物料号 {MaterialNumber} 失败");
                _messageService.ShowError($"查询失败，当前工单和菜单保持不变: {ex.Message}", "错误");
            }

            // 制令单查询过程中由 MES 临时回填的物料号不能残留，否则重试会被误判为手动物料查询
            if (!isManualMaterialQuery)
                MaterialNumber = string.Empty;
        }
        finally
        {
            IsQuerying = false;
        }
    }

    /// <inheritdoc/>
    public bool IsNavigationTarget(NavigationContext navigationContext) => true;
    /// <inheritdoc/>
    public void OnNavigatedFrom(NavigationContext navigationContext) { }
    /// <inheritdoc/>
    public void OnNavigatedTo(NavigationContext navigationContext) { }
}
