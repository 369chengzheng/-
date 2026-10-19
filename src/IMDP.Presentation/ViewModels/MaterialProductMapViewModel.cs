// =============================================================================
// 文件名：MaterialProductMapViewModel.cs
// 功能描述：物料-产品映射维护视图模型。
//           按软件物料号分页查询映射记录，支持新增/修改/软删除，
//           并以分组复选框形式维护每个物料号启用的产品插件集合。
//           继承 PaginationViewModelBase<MaterialProductMapDto>，复用分页状态与翻页命令，仅实现 RefreshAsync()。
// =============================================================================
using System.Windows;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Events;
using Newtonsoft.Json;
using IMDP.Application.Services;
using IMDP.Application.DTOs;
using IMDP.Domain.Entities;
using IMDP.Domain.Interfaces;
using IMDP.Domain.Products;

namespace IMDP.Presentation.ViewModels;

/// <summary>
/// 物料-产品映射维护视图模型
/// <para>调用 <see cref="IMaterialProductMapRepository"/> 实现分页查询/新增/修改/软删除；
/// 通过 <see cref="IProductPluginRegistry"/> 加载所有可用插件并以分组复选框呈现。</para>
/// <para>通过 <see cref="ICurrentContext"/> 获取当前工号，用于填充审计字段。</para>
/// </summary>
public partial class MaterialProductMapViewModel : PaginationViewModelBase<MaterialProductMapDto>
{
    private readonly IMaterialProductMapRepository _mapRepository;
    private readonly IProductPluginRegistry _pluginRegistry;
    private readonly ICurrentContext _currentContext;
    private readonly IMessageService _messageService;
    private readonly SemaphoreSlim _databaseOperationGate = new(1, 1);

    // 当前登录用户工号（用于审计字段）
    private string _currentWorkNumber = string.Empty;

    /// <summary>物料-产品映射记录集合（当前页）</summary>
    [ObservableProperty]
    private ObservableCollection<MaterialProductMapDto> _maps = new();

    /// <summary>当前选中的映射记录</summary>
    [ObservableProperty]
    private MaterialProductMapDto? _selectedMap;

    // 查询条件：软件物料号
    [ObservableProperty]
    private string _materialNumberKeyword = string.Empty;

    // 产品插件配置区：按分类分组的复选框列表
    public ObservableCollection<PluginGroupVm> PluginGroups { get; } = new();

    /// <summary>查询命令（按物料号关键字拉取首页数据）</summary>
    public IAsyncRelayCommand QueryCommand { get; }
    /// <summary>维护命令（新增或修改当前映射的插件配置）</summary>
    public IRelayCommand MaintainCommand { get; }
    /// <summary>删除命令（软删除选中的映射记录）</summary>
    public IRelayCommand DeleteCommand { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="mapRepository">物料-产品映射仓储</param>
    /// <param name="pluginRegistry">产品插件注册表</param>
    /// <param name="eventAggregator">Prism 事件聚合器</param>
    /// <param name="currentContext">当前登录用户与运行环境上下文</param>
    /// <param name="messageService">统一消息提示服务</param>
    public MaterialProductMapViewModel(
        IMaterialProductMapRepository mapRepository,
        IProductPluginRegistry pluginRegistry,
        IEventAggregator eventAggregator,
        IMessageService messageService,
        ICurrentContext currentContext)
        : base(eventAggregator)
    {
        _messageService = messageService;
        _mapRepository = mapRepository;
        _pluginRegistry = pluginRegistry;
        _currentContext = currentContext;

        //重置到首页后按物料号关键字查询
        QueryCommand = new AsyncRelayCommand(ResetToFirstPageAndRefreshAsync, CanExecuteDatabaseOperation);
        MaintainCommand = new AsyncRelayCommand(() => RunDatabaseOperationAsync(OnMaintainAsync, true), CanExecuteDatabaseOperation);
        DeleteCommand = new AsyncRelayCommand(() => RunDatabaseOperationAsync(OnDeleteAsync, true), CanExecuteDatabaseOperation);

        //此时登陆广播早已结束，订阅不到登陆事件，改用调用已经保存好的 ICurrentContext.UserNam
        // 订阅登录事件，保存当前工号供审计字段使用
        //eventAggregator.GetEvent<UserLoggedInEvent>().Subscribe(OnUserLoggedIn, ThreadOption.UIThread);

        LoadPluginGroups();
    }

    /// <summary>
    /// 用户登录成功回调：保存当前工号
    /// </summary>
    /// <param name="payload">登录事件负载</param>
    //private void OnUserLoggedIn(UserLoggedInPayload payload)
    //{
    //    _currentWorkNumber = payload.WorkNumber;
    //}

    /// <summary>
    /// 从插件注册表加载全部产品插件，按 Category 分组生成复选框列表。
    /// </summary>
    private void LoadPluginGroups()
    {
        PluginGroups.Clear();
        var plugins = _pluginRegistry.GetAllPlugins();
        foreach (var group in plugins.GroupBy(p => p.Category))
        {
            var groupVm = new PluginGroupVm(group.Key);
            foreach (var plugin in group)
            {
                groupVm.Items.Add(new PluginItemVm
                {
                    ProductKey = plugin.ProductKey,
                    DisplayName = string.IsNullOrWhiteSpace(plugin.DisplayName) ? plugin.ProductKey : plugin.DisplayName
                });
            }
            PluginGroups.Add(groupVm);
        }
    }

    /// <summary>
    /// 执行查询：按物料号关键字分页拉取记录并转为 DTO
    /// </summary>
    public override Task RefreshAsync() => RunDatabaseOperationAsync(RefreshCoreAsync);

    // 维护和删除已持有执行锁，刷新列表时直接调用核心方法，避免重复获取同一把锁。
    private async Task RefreshCoreAsync()
    {
        var result = await _mapRepository.GetPagedAsync(MaterialNumberKeyword ?? string.Empty, PageIndex, PageSize);

        // ProductKey → 插件显示名称，供列表列与悬浮框做中文展示
        var pluginNames = _pluginRegistry.GetAllPlugins()
            .GroupBy(p => p.ProductKey)
            .ToDictionary(g => g.Key, g => g.First().DisplayName);

        Maps = new ObservableCollection<MaterialProductMapDto>(result.Items.Select(e => MapToDto(e, pluginNames)));
        TotalCount = result.TotalCount;
    }

    private bool CanExecuteDatabaseOperation() => _databaseOperationGate.CurrentCount > 0;

    /// <summary>本页数据库操作串行执行；重复写入直接忽略，分页刷新等待前一次操作完成。</summary>
    private async Task RunDatabaseOperationAsync(Func<Task> operation, bool skipIfBusy = false)
    {
        if (skipIfBusy)
        {
            if (!await _databaseOperationGate.WaitAsync(0)) return;
        }
        else
        {
            await _databaseOperationGate.WaitAsync();
        }

        try
        {
            NotifyDatabaseCommands();
            await operation();
        }
        catch (Exception ex)
        {
            _messageService.ShowError($"物料操作失败：{ex.Message}", "错误");
        }
        finally
        {
            _databaseOperationGate.Release();
            NotifyDatabaseCommands();
        }
    }

    private void NotifyDatabaseCommands()
    {
        QueryCommand.NotifyCanExecuteChanged();
        MaintainCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 维护：未选中记录时新增（提示输入软件物料号），已选中时保存当前勾选的插件配置。
    /// </summary>
    private async Task OnMaintainAsync()
    {
        // 收集当前所有勾选的插件 ProductKey 并序列化为 JSON
        var selectedKeys = PluginGroups.SelectMany(g => g.Items).Where(i => i.IsChecked).Select(i => i.ProductKey).ToList();
        var pluginsJson = JsonConvert.SerializeObject(selectedKeys);
        var currentWorkNumber = _currentContext.UserName;

        if (SelectedMap == null)
        {
            // 新增
            var materialNumber = UserManageViewModel.ShowInputDialog("新增物料产品映射", "请输入软件物料号：", string.Empty);
            if (string.IsNullOrWhiteSpace(materialNumber))
            {
                if (materialNumber == null) return; // 用户取消
                _messageService.ShowWarning("软件物料号不能为空", "警告");
                return;
            }

            if (await _mapRepository.MaterialNumberExistsAsync(materialNumber))
            {
                _messageService.ShowWarning("软件物料号已存在", "警告");
                return;
            }

            var entity = new MaterialProductMap
            {
                MaterialNumber = materialNumber,
                ProductPluginsJson = pluginsJson,
                CreatedBy = currentWorkNumber,
                CreatedTime = DateTime.Now,
                UpdatedBy = currentWorkNumber,
                UpdatedTime = DateTime.Now
            };
            await _mapRepository.AddAsync(entity);
            _messageService.ShowInfo("新增成功", "提示");
        }
        else
        {
            // 修改：保存当前勾选的插件配置
            var entity = await _mapRepository.GetByIdAsync(SelectedMap.Id);
            if (entity == null)
            {
                _messageService.ShowWarning("记录不存在或已被删除", "警告");
                return;
            }

            entity.ProductPluginsJson = pluginsJson;
            entity.UpdatedBy = currentWorkNumber;
            entity.UpdatedTime = DateTime.Now;
            await _mapRepository.UpdateAsync(entity);
            _messageService.ShowInfo("修改成功", "提示");
        }

        await RefreshCoreAsync();
    }

    /// <summary>
    /// 删除选中记录（软删除：置 <see cref="MaterialProductMap.IsDeleted"/> 为 true）
    /// </summary>
    private async Task OnDeleteAsync()
    {
        if (SelectedMap == null)
        {
            _messageService.ShowWarning("请先选择要删除的映射记录", "警告");
            return;
        }

        if (!_messageService.ShowConfirm($"确认删除软件物料号 {SelectedMap.MaterialNumber} 的映射吗？", "确认删除")) return;

        var entity = await _mapRepository.GetByIdAsync(SelectedMap.Id);
        if (entity == null)
        {
            _messageService.ShowWarning("记录不存在", "警告");
            return;
        }

        // 软删除
        entity.IsDeleted = true;
        entity.UpdatedBy = _currentContext.UserName;
        entity.UpdatedTime = DateTime.Now;
        await _mapRepository.UpdateAsync(entity);

        _messageService.ShowInfo("删除成功", "提示");
        SelectedMap = null;
        await RefreshCoreAsync();
    }

    /// <summary>
    /// 选中记录变化时，根据记录的 ProductPluginsJson 同步复选框勾选状态。
    /// </summary>
    partial void OnSelectedMapChanged(MaterialProductMapDto? value)
    {
        var keys = new HashSet<string>();
        if (value != null && !string.IsNullOrWhiteSpace(value.ProductPluginsJson))
        {
            try
            {
                var list = JsonConvert.DeserializeObject<List<string>>(value.ProductPluginsJson);
                if (list != null)
                {
                    foreach (var k in list) keys.Add(k);
                }
            }
            catch { }
        }

        foreach (var group in PluginGroups)
        {
            foreach (var item in group.Items)
            {
                item.IsChecked = keys.Contains(item.ProductKey);
            }
        }
    }

    /// <summary>
    /// 将实体转为 DTO（隔离领域实体与视图层模型）
    /// </summary>
    /// <param name="entity">领域实体</param>
    /// <param name="pluginNames">ProductKey → 插件显示名称</param>
    /// <returns>视图层 DTO</returns>
    private static MaterialProductMapDto MapToDto(MaterialProductMap entity, IReadOnlyDictionary<string, string> pluginNames)
    {
        return new MaterialProductMapDto
        {
            Id = entity.Id,
            MaterialNumber = entity.MaterialNumber,
            ProductPluginsJson = entity.ProductPluginsJson,
            ProductPluginsDisplay = FormatPlugins(entity.ProductPluginsJson, pluginNames, "、"),
            ProductPluginsToolTip = FormatPlugins(entity.ProductPluginsJson, pluginNames, "\n"),
            CreatedBy = entity.CreatedBy,
            CreatedTime = entity.CreatedTime,
            UpdatedBy = entity.UpdatedBy,
            UpdatedTime = entity.UpdatedTime
        };
    }

    /// <summary>
    /// 将 ProductPluginsJson 翻译为"显示名（ProductKey）"格式文本
    /// <para>找不到显示名的键原样保留；JSON 无效时回退显示原始内容</para>
    /// </summary>
    /// <param name="productPluginsJson">ProductKey 数组的 JSON 字符串</param>
    /// <param name="pluginNames">ProductKey → 插件显示名称</param>
    /// <param name="separator">多项之间的分隔符（列表用"、"，悬浮框用换行）</param>
    private static string FormatPlugins(string productPluginsJson, IReadOnlyDictionary<string, string> pluginNames, string separator)
    {
        if (string.IsNullOrWhiteSpace(productPluginsJson))
            return string.Empty;

        List<string>? keys;
        try
        {
            keys = JsonConvert.DeserializeObject<List<string>>(productPluginsJson);
        }
        catch (JsonException)
        {
            return productPluginsJson;
        }

        if (keys == null || keys.Count == 0)
            return string.Empty;

        var items = keys.Select(key =>
            pluginNames.TryGetValue(key, out var name) && !string.IsNullOrWhiteSpace(name)
                ? $"{name}（{key}）"
                : key);

        return string.Join(separator, items);
    }

    /// <inheritdoc/>
    /// <remarks>进入页面时若插件列表为空则重新加载（插件可能在运行时动态注册）</remarks>
    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        // 进入页面时若插件列表为空则重新加载（插件可能在运行时动态注册）
        if (PluginGroups.Count == 0)
        {
            LoadPluginGroups();
        }
    }
}

/// <summary>
/// 产品插件分组视图模型（按 Category 分组）。
/// </summary>
public class PluginGroupVm : ObservableObject
{
    /// <summary>分组类别名称</summary>
    public string Category { get; }

    /// <summary>分组类别的中文显示名称</summary>
    public string CategoryDisplayName => ProductCategory.GetDisplayName(Category);

    /// <summary>分组下的插件项集合</summary>
    public ObservableCollection<PluginItemVm> Items { get; } = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="category">分组类别名称</param>
    public PluginGroupVm(string category)
    {
        Category = category;
    }
}

/// <summary>
/// 产品插件项视图模型（复选框）。
/// </summary>
public partial class PluginItemVm : ObservableObject
{
    /// <summary>产品唯一标识（与插件 ProductKey 对应）</summary>
    public string ProductKey { get; set; } = string.Empty;

    /// <summary>显示名称（若插件未配置则回退为 ProductKey）</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>是否启用该插件（与复选框勾选状态双向绑定）</summary>
    [ObservableProperty]
    private bool _isChecked;
}
