// =============================================================================
// 文件名：PaginationViewModelBase.cs
// 功能描述：分页查询视图模型基类。
//           提取 BoardQuery/WholeQuery/BoardConfigHistory/WholeConfigHistory/
//           LoginQuery/UpdateQuery/OrderSwitchQuery/UserManage/MaterialProductMap
//           等 9 个分页 ViewModel 的通用逻辑（页码状态、翻页命令、返回主页、INavigationAware），
//           子类只需实现 RefreshAsync() 提供具体查询与数据填充逻辑。
// =============================================================================
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Events;
using Prism.Navigation.Regions;
using IMDP.Application.Events;

namespace IMDP.Presentation.ViewModels;

/// <summary>
/// 分页查询视图模型基类
/// <para>封装分页状态（Page Index/Size/Total Count）、翻页命令（First/Previous/Next/Last）、
/// 返回主页命令（GoHome）与 Prism 导航回调默认实现。</para>
/// <para>子类通过实现 <see cref="RefreshAsync"/> 提供具体查询逻辑；翻页与每页条数变化时自动触发刷新。</para>
/// </summary>
/// <typeparam name="T">分页数据项类型</typeparam>
public abstract partial class PaginationViewModelBase<T> : ObservableObject, INavigationAware
{
    /// <summary>Prism 事件聚合器（用于发布返回主页事件）</summary>
    private readonly IEventAggregator _eventAggregator;

    /// <summary>
    /// 重置页码时暂时关闭自动刷新，避免同一次点击查询两次数据库
    /// </summary>
    private bool _suppressAutomaticRefresh;

    /// <summary>当前页码（从 1 开始；变化时联动 <see cref="TotalPages"/> 并触发刷新）</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    private int _pageIndex = 1;

    /// <summary>每页条数（默认 20，变化时联动 <see cref="TotalPages"/> 并重置到首页）</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    private int _pageSize = 20;

    /// <summary>符合查询条件的总记录数（由服务端返回）</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    private int _totalCount;

    /// <summary>总页数（根据 TotalCount 与 PageSize 计算；TotalCount 为 0 时为 1）</summary>
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>每页条数可选项</summary>
    public int[] PageSizeOptions { get; } = { 10, 20, 50, 100 };

    /// <summary>返回主页命令</summary>
    public IRelayCommand GoHomeCommand { get; }

    /// <summary>跳转到首页命令</summary>
    public IRelayCommand FirstPageCommand { get; }

    /// <summary>上一页命令</summary>
    public IRelayCommand PreviousPageCommand { get; }

    /// <summary>下一页命令</summary>
    public IRelayCommand NextPageCommand { get; }

    /// <summary>跳转到末页命令</summary>
    public IRelayCommand LastPageCommand { get; }

    /// <summary>
    /// 构造函数，初始化翻页命令
    /// </summary>
    /// <param name="eventAggregator">Prism 事件聚合器（用于发布返回主页事件）</param>
    protected PaginationViewModelBase(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
        GoHomeCommand = new RelayCommand(OnGoHome);
        FirstPageCommand = new RelayCommand(() => { if (PageIndex > 1) PageIndex = 1; });
        PreviousPageCommand = new RelayCommand(() => { if (PageIndex > 1) PageIndex--; });
        NextPageCommand = new RelayCommand(() => { if (PageIndex < TotalPages) PageIndex++; });
        LastPageCommand = new RelayCommand(() => { if (PageIndex < TotalPages) PageIndex = TotalPages; });
    }

    // 页码变化时自动触发子类实现的查询逻辑（fire-and-forget；异常由 Task 捕获，避免 async void 导致进程崩溃）
    partial void OnPageIndexChanged(int value)
    {
        if (!_suppressAutomaticRefresh)
            _ = RefreshAsync();
    }

    // 每页条数变化时重置到首页；原本已在首页时也必须主动刷新。
    partial void OnPageSizeChanged(int value)
    {
        if (PageIndex != 1)
            PageIndex = 1;
        else
            _ = RefreshAsync();
    }

    /// <summary>
    /// 将新查询重置到第一页并只刷新一次
    /// </summary>
    protected async Task ResetToFirstPageAndRefreshAsync()
    {
        if (PageIndex != 1)
        {
            _suppressAutomaticRefresh = true;
            try
            {
                PageIndex = 1;
            }
            finally
            {
                _suppressAutomaticRefresh = false;
            }
        }

        await RefreshAsync();
    }

    /// <summary>
    /// 返回主页处理：发布 <see cref="GoHomeEvent"/>
    /// </summary>
    private void OnGoHome()
    {
        _eventAggregator.GetEvent<GoHomeEvent>().Publish();
    }

    /// <summary>
    /// 子类实现：按当前查询条件与分页参数执行查询，并填充子类自己的记录集合。
    /// <para>由基类的翻页命令、页码/页大小变化自动调用；子类也可在 QueryCommand 中直接调用。</para>
    /// <para>异常处理：返回 Task 后由调用方决定是否 await；fire-and-forget 场景下异常将被 Task 捕获，
    /// 不会像 async void 那样直接传播至 SynchronizationContext 导致应用崩溃。</para>
    /// </summary>
    public abstract Task RefreshAsync();

    /// <inheritdoc/>
    public virtual bool IsNavigationTarget(NavigationContext navigationContext) => true;

    /// <inheritdoc/>
    public virtual void OnNavigatedFrom(NavigationContext navigationContext) { }

    /// <inheritdoc/>
    /// <remarks>基类默认不自动查询；子类可重写以决定首次进入时是否触发 <see cref="RefreshAsync"/>。</remarks>
    public virtual void OnNavigatedTo(NavigationContext navigationContext) { }
}
