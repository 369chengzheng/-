using IMDP.Application.Events;
using IMDP.Domain.Products;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace IMDP.Products.GasDG.AlarmHost.ViewModels;

/// <summary>插件骨架页面的只读上下文；不加载方案或执行检测。</summary>
public abstract class AlarmHostPageViewModel : BindableBase, INavigationAware
{
    private string _materialNumber = string.Empty;
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

    public string PageTitle
    {
        get => _pageTitle;
        protected set
        {
            if (SetProperty(ref _pageTitle, value)) RaisePropertyChanged(nameof(BreadcrumbTitle));
        }
    }
    public string ProductKey { get; }
    public string Description { get; }
    public string StatusText => "功能尚未实现";
    public string StageNotice => "当前仅验证插件加载、菜单和页面导航。方案查询与维护、设备通信、检测判定及 MES 上传尚未接入。";
    public DelegateCommand GoHomeCommand { get; }

    public string MaterialNumber
    {
        get => _materialNumber;
        private set
        {
            if (SetProperty(ref _materialNumber, value))
            {
                RaisePropertyChanged(nameof(BreadcrumbTitle));
                RaisePropertyChanged(nameof(MaterialNumberDisplay));
            }
        }
    }

    public string BreadcrumbTitle => string.IsNullOrEmpty(MaterialNumber)
        ? PageTitle
        : $"{MaterialNumber} > {PageTitle}";

    public string MaterialNumberDisplay => string.IsNullOrEmpty(MaterialNumber)
        ? "未提供（请先在首页查询工单或软件物料号）"
        : MaterialNumber;

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public virtual void OnNavigatedTo(NavigationContext navigationContext)
    {
        // 缺少物料参数时也清除旧值，避免页面重用后显示上一次物料。
        MaterialNumber = (navigationContext.Parameters.GetValue<object>("MaterialNumber") as string)?.Trim() ?? string.Empty;
    }

    public void OnNavigatedFrom(NavigationContext navigationContext) { }
}
