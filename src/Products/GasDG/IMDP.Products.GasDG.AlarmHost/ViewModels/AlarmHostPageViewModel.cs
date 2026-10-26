using IMDP.Application.Events;
using IMDP.Domain.Products;
using Prism.Commands;
using Prism.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Navigation.Regions;

namespace IMDP.Products.GasDG.AlarmHost.ViewModels;

/// <summary>
/// 报警页面共用的物料上下文、标题及首页导航
/// </summary>
public abstract class AlarmHostPageViewModel : ObservableObject, INavigationAware
{

    /// <summary>
    /// 当前导航物料号的绑定存储值
    /// </summary>
    private string _materialNumber = string.Empty;
    /// <summary>
    /// 首页制令单查询返回的MES产品名称，手动物料导航不携带
    /// </summary>
    private string _productName = string.Empty;
    /// <summary>
    /// 当前页面标题的绑定存储值
    /// </summary>
    private string _pageTitle = string.Empty;

    protected AlarmHostPageViewModel(
        GasDGAlarmHostPlugin plugin,
        IEventAggregator eventAggregator,
        FunctionModuleType moduleType)
    {
        PageTitle = plugin.GetMenuDisplayName(moduleType);
        ProductKey = plugin.ProductKey;
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
            }
        }
    }

    /// <summary>
    /// 首页导航传入的MES产品名称，只用于显示，不参与方案定位
    /// </summary>
    public string ProductName
    {
        get => _productName;
        private set
        {
            if (SetProperty(ref _productName, value)) OnPropertyChanged(nameof(BreadcrumbTitle));
        }
    }

    /// <summary>
    /// 按物料号、MES产品名称及当前页面名组合导航，空产品名称不占一层
    /// </summary>
    public string BreadcrumbTitle => string.Join(" > ",
        new[] { MaterialNumber, ProductName, PageTitle }.Where(value => !string.IsNullOrEmpty(value)));

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public virtual void OnNavigatedTo(NavigationContext navigationContext)
    {
        // 缺少物料参数时也清除旧值，避免页面重用后显示上一次物料。
        MaterialNumber = (navigationContext.Parameters.GetValue<object>("MaterialNumber") as string)?.Trim() ?? string.Empty;
        ProductName = (navigationContext.Parameters.GetValue<object>("ProductName") as string)?.Trim() ?? string.Empty;
    }

    public virtual void OnNavigatedFrom(NavigationContext navigationContext) { }
}
