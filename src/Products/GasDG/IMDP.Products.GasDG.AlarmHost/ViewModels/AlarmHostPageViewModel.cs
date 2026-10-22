using IMDP.Application.Events;
using IMDP.Domain.Products;
using Prism.Commands;
using Prism.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Navigation.Regions;

namespace IMDP.Products.GasDG.AlarmHost.ViewModels;

/// <summary>
/// 插件骨架页面的只读上下文；不加载方案或执行检测。
/// </summary>
public abstract class AlarmHostPageViewModel : ObservableObject, INavigationAware
{

    /// <summary>
    /// 当前导航物料号的绑定存储值
    /// </summary>
    private string _materialNumber = string.Empty;
    /// <summary>
    /// 当前页面标题的绑定存储值
    /// </summary>
    private string _pageTitle = string.Empty;

    protected AlarmHostPageViewModel(
        GasDGAlarmHostPlugin plugin,
        IEventAggregator eventAggregator,
        FunctionModuleType moduleType,
        string description)
    {
        PageTitle = plugin.GetMenuDisplayName(moduleType);
        ProductKey = plugin.ProductKey;
        Description = description;
        GoHomeCommand = new DelegateCommand(() => eventAggregator.GetEvent<GoHomeEvent>().Publish());
    }

    /// <summary>
    /// 当前页面标题，随设置或检验入口更新
    /// </summary>
    public string PageTitle
    {
        get => _pageTitle;
        protected set
        {
            if (SetProperty(ref _pageTitle, value)) OnPropertyChanged(nameof(BreadcrumbTitle));
        }
    }
    /// <summary>
    /// 当前页面所属的产品插件标识
    /// </summary>
    public string ProductKey { get; }
    /// <summary>
    /// 当前页面的功能说明文本
    /// </summary>
    public string Description { get; }
    /// <summary>
    /// 执行页骨架的功能状态提示
    /// </summary>
    public string StatusText => "功能尚未实现";
    /// <summary>
    /// 当前基础阶段已接入范围及待接入功能的提示
    /// </summary>
    public string StageNotice => "当前仅验证插件加载、菜单和页面导航。方案查询与维护、设备通信、检测判定及 MES 上传尚未接入。";
    /// <summary>
    /// 返回首页的导航命令
    /// </summary>
    public DelegateCommand GoHomeCommand { get; }

    /// <summary>
    /// 首页导航传入的软件物料号
    /// </summary>
    public string MaterialNumber
    {
        get => _materialNumber;
        private set
        {
            if (SetProperty(ref _materialNumber, value))
            {
                OnPropertyChanged(nameof(BreadcrumbTitle));
                OnPropertyChanged(nameof(MaterialNumberDisplay));
            }
        }
    }

    /// <summary>
    /// 由当前物料号和页面标题组成的面包屑文本
    /// </summary>
    public string BreadcrumbTitle => string.IsNullOrEmpty(MaterialNumber)
        ? PageTitle
        : $"{MaterialNumber} > {PageTitle}";

    /// <summary>
    /// 物料号的页面显示文本，缺失时给出首页操作提示
    /// </summary>
    public string MaterialNumberDisplay => string.IsNullOrEmpty(MaterialNumber)
        ? "未提供（请先在首页查询工单或软件物料号）"
        : MaterialNumber;

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public virtual void OnNavigatedTo(NavigationContext navigationContext)
    {
        // 缺少物料参数时也清除旧值，避免页面重用后显示上一次物料。
        MaterialNumber = (navigationContext.Parameters.GetValue<object>("MaterialNumber") as string)?.Trim() ?? string.Empty;
    }

    public virtual void OnNavigatedFrom(NavigationContext navigationContext) { }
}
