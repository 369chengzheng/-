// =============================================================================
// 文件名：LaserDetectorConfigViewModel.cs
// 功能描述：激光检测仪模块检测方案配置视图模型（严格 MVVM）。
//           维护 19 个检测参数项（含 MES 代码），通过 ITestPlanService 实现方案的
//           分页查询、保存与删除；用户提示统一经 IMessageService（Growl/对话框），
//           操作员取自 ICurrentContext，ViewModel 不含任何 WPF/UI 引用。
//           命令与属性全部由 CommunityToolkit.Mvvm 源生成器生成
//           （[RelayCommand] / [ObservableProperty] / [NotifyPropertyChangedFor] /
//            [NotifyCanExecuteChangedFor]），无手写样板代码。
// =============================================================================
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Events;
using IMDP.Application.DTOs;
using IMDP.Application.Services;
using IMDP.Application.Events;
using IMDP.Domain.Interfaces;
using IMDP.Domain.Products;
using IMDP.Products.GasDG.LaserDetector.Models;

namespace IMDP.Products.GasDG.LaserDetector.ViewModels;

/// <summary>
/// 激光检测仪模块检测方案配置视图模型
/// <para>参数配置区以 <see cref="TestParameterDto"/> 列表承载 19 个检测项，
/// 每项的子参数按取值方式生成：Range 用 VALUE/LOWER_LIMIT/UPPER_LIMIT，
/// Value 用 VALUE，TestMode 用 VALUE（01/02），Status 无子参数。</para>
/// </summary>
public partial class LaserDetectorConfigViewModel : ObservableObject, INavigationAware
{
    #region 字段

    private readonly ITestPlanService _testPlanService;
    private readonly IMessageService _messageService;
    private readonly ICurrentContext _currentContext;
    private readonly IEventAggregator _eventAggregator;

    #endregion

    #region 可观察属性

    /// <summary>当前软件物料号（由主界面导航时传入，用于面包屑显示）</summary>
    [ObservableProperty]
    private string _currentMaterialNumber = string.Empty;

    /// <summary>产品编码（查询与保存的主键）</summary>
    [ObservableProperty]
    private string _productCode = string.Empty;

    /// <summary>工位数量（来源数据库方案配置，不设上限）</summary>
    [ObservableProperty]
    private int _stationCount = 3;

    /// <summary>
    /// 单工位检测最大超时时间（秒，默认 120）
    /// <para>约束该工位从下发开始测试指令到收到结果帧的总耗时，
    /// 多工位共享串口导致轮询节奏变慢时由产线按需调大。</para>
    /// </summary>
    [ObservableProperty]
    private int _maxTestTimeout = 120;

    /// <summary>
    /// 参数配置区参数列表（19 个激光检测参数项；集合变化时通知启用项数统计刷新）
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnabledCount))]
    private ObservableCollection<TestParameterDto> _parameters = new();

    /// <summary>方案列表（分页查询结果）</summary>
    [ObservableProperty]
    private ObservableCollection<TestPlanDto> _plans = new();

    /// <summary>当前选中的方案</summary>
    [ObservableProperty]
    private TestPlanDto? _selectedPlan;

    /// <summary>
    /// 当前页码（从 1 开始；变化时联动刷新分页命令可用性）
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FirstPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LastPageCommand))]
    private int _pageIndex = 1;

    /// <summary>
    /// 每页条数（变化时联动刷新总页数与分页命令可用性）
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    [NotifyCanExecuteChangedFor(nameof(FirstPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LastPageCommand))]
    private int _pageSize = 10;

    /// <summary>
    /// 总记录数（变化时联动刷新总页数与分页命令可用性）
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    [NotifyCanExecuteChangedFor(nameof(FirstPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LastPageCommand))]
    private int _totalCount;

    #endregion

    #region 计算属性

    /// <summary>
    /// 总页数（由 TotalCount 与 PageSize 计算）
    /// </summary>
    public int TotalPages => PageSize > 0
        ? (TotalCount + PageSize - 1) / PageSize
        : 0;

    /// <summary>
    /// 已启用检测项数（用于参数配置区标题栏显示，如 8/19）
    /// </summary>
    public int EnabledCount => Parameters.Count(parameter => parameter.IsEnabled);

    /// <summary>
    /// 页大小下拉选项
    /// </summary>
    public IReadOnlyList<int> PageSizeOptions { get; } = new[] { 10, 20, 50 };

    /// <summary>首页命令可用性（非第一页时可用）</summary>
    private bool CanFirstPage => PageIndex > 1;

    /// <summary>上一页命令可用性（非第一页时可用）</summary>
    private bool CanPreviousPage => PageIndex > 1;

    /// <summary>下一页命令可用性（非末页时可用）</summary>
    private bool CanNextPage => PageIndex < TotalPages;

    /// <summary>末页命令可用性（非末页时可用）</summary>
    private bool CanLastPage => PageIndex < TotalPages;

    #endregion

    #region 构造函数

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="testPlanService">检测计划服务（方案增删改查）</param>
    /// <param name="messageService">统一消息提示服务（Growl 轻提示/确认对话框）</param>
    /// <param name="currentContext">当前用户上下文（提供操作员工号）</param>
    /// <param name="eventAggregator">事件聚合器（发布返回首页事件）</param>
    public LaserDetectorConfigViewModel(
        ITestPlanService testPlanService,
        IMessageService messageService,
        ICurrentContext currentContext,
        IEventAggregator eventAggregator)
    {
        _testPlanService = testPlanService;
        _messageService = messageService;
        _currentContext = currentContext;
        _eventAggregator = eventAggregator;

        InitializeDefaultParameters();
    }

    #endregion

    #region 业务命令

    /// <summary>
    /// 查询命令：重置到第一页并按产品编码分页查询方案（结果为空时轻提示）
    /// </summary>
    [RelayCommand]
    private async Task QueryAsync()
    {
        PageIndex = 1;
        await OnQueryAsync(notifyEmpty: true);
    }

    /// <summary>
    /// 按编码加载方案命令（产品编码失焦或键入空格触发）
    /// <para>存在该编码的方案时回填编辑区并轻提示成功；不存在时轻提示。</para>
    /// </summary>
    [RelayCommand]
    private async Task LoadByCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(ProductCode))
        {
            return;
        }

        var plan = await _testPlanService.GetBoardConfigAsync(ProductCode, ProductCategory.GasDG);
        if (plan == null)
        {
            _messageService.GrowlInfo($"产品编码 [{ProductCode}] 不存在配置！");
            return;
        }

        ApplyPlan(plan);
        _messageService.GrowlSuccess($"已加载产品编码 [{ProductCode}] 的配置。");
    }

    /// <summary>
    /// 维护命令：保存当前编辑区参数为方案（新增或更新）
    /// <para>成功时 Growl 成功轻提示（区分新增/更新）；失败时 Growl 信息提示错误原因；
    /// 校验不通过时 Growl 信息提示。</para>
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(ProductCode))
        {
            _messageService.GrowlInfo("请输入产品编码！");
            return;
        }

        if (StationCount < 1)
        {
            _messageService.GrowlInfo("工位数量至少为 1！");
            return;
        }

        if (MaxTestTimeout < 1)
        {
            _messageService.GrowlInfo("最大超时时间至少为 1 秒！");
            return;
        }

#if !DEBUG
        // Release 构建下：勾选启用的检测项必须配置 MES 代码（生产环境需与 MES 系统对接）；
        // Debug 构建下允许为空，便于开发调试。逐项收集后一次性提示，方便用户定位补齐。
        var missingMesCodeNames = Parameters
            .Where(p => p.IsEnabled && string.IsNullOrWhiteSpace(p.MesCode))
            .Select(p => p.Name)
            .ToList();
        if (missingMesCodeNames.Count > 0)
        {
            _messageService.GrowlInfo($"以下已启用的检测项未配置 MES 代码：{string.Join("、", missingMesCodeNames)}！");
            return;
        }
#endif

        try
        {
            // 判断是新增还是更新：按编码查询已存在方案
            bool isUpdate = await _testPlanService.GetBoardConfigAsync(ProductCode, ProductCategory.GasDG) != null;

            // 单工位最大超时时间与 12V 电压等检测项一样作为参数项存入 ParametersJson
            //（先过滤旧超时项再注入，避免重复；服务层与数据库均无感知）
            var parameters = Parameters
                .Where(p => p.Code != LaserDetectorItemCodes.MaxTestTimeout)
                .ToList();
            LaserDetectorTimeoutParameter.Upsert(parameters, MaxTestTimeout);

            var dto = new TestPlanDto
            {
                ProductCode = ProductCode,
                StationCount = StationCount,
                Parameters = parameters,
                Category = ProductCategory.GasDG,
                UpdatedBy = _currentContext.UserName,
                UpdatedTime = DateTime.Now
            };

            bool success = await _testPlanService.SaveBoardConfigAsync(dto, _currentContext.UserName);
            if (!success)
            {
                _messageService.GrowlInfo($"方案保存失败：未知产品类别 [{ProductCategory.GasDG}]！");
                return;
            }

            _messageService.GrowlSuccess(isUpdate
                ? $"产品编码 [{ProductCode}] 的方案更新成功！"
                : $"产品编码 [{ProductCode}] 的方案保存成功！");
            await OnQueryAsync();
        }
        catch (Exception ex)
        {
            _messageService.GrowlInfo($"方案保存失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 删除命令：删除选中的方案（Growl 是/否确认 + 成功/失败轻提示）
    /// <para>确认采用非阻塞 Growl.Ask：用户点击“是”后经回调执行删除，
    /// 替代原阻塞式 MessageBox 确认框。</para>
    /// </summary>
    [RelayCommand]
    private void Delete()
    {
        if (SelectedPlan == null)
        {
            _messageService.GrowlInfo("请选中要删除的方案！");
            return;
        }

        _messageService.GrowlConfirm($"确认删除产品编码 [{SelectedPlan.ProductCode}] 的方案？", async isConfirmed =>
        {
            if (!isConfirmed)
            {
                return;
            }

            // 回调前再取一次选中方案，避免确认期间选择已变化
            var plan = SelectedPlan;
            if (plan == null)
            {
                return;
            }

            try
            {
                bool success = await _testPlanService.DeleteBoardConfigAsync(plan.Id, _currentContext.UserName, ProductCategory.GasDG);
                if (!success)
                {
                    _messageService.GrowlInfo("方案删除失败：方案不存在或已被删除！");
                    return;
                }

                _messageService.GrowlSuccess($"产品编码 [{plan.ProductCode}] 的方案删除成功！");
                await OnQueryAsync();
            }
            catch (Exception ex)
            {
                _messageService.GrowlInfo($"方案删除失败：{ex.Message}");
            }
        });
    }

    /// <summary>返回首页命令（发布返回首页事件）</summary>
    [RelayCommand]
    private void GoHome()
    {
        _eventAggregator.GetEvent<GoHomeEvent>().Publish();
    }

    #endregion

    #region 分页命令

    /// <summary>分页命令：跳转首页</summary>
    [RelayCommand(CanExecute = nameof(CanFirstPage))]
    private void FirstPage() => NavigateToPage(1);

    /// <summary>分页命令：跳转上一页</summary>
    [RelayCommand(CanExecute = nameof(CanPreviousPage))]
    private void PreviousPage() => NavigateToPage(PageIndex - 1);

    /// <summary>分页命令：跳转下一页</summary>
    [RelayCommand(CanExecute = nameof(CanNextPage))]
    private void NextPage() => NavigateToPage(PageIndex + 1);

    /// <summary>分页命令：跳转末页</summary>
    [RelayCommand(CanExecute = nameof(CanLastPage))]
    private void LastPage() => NavigateToPage(TotalPages);

    #endregion

    #region 属性变化回调

    /// <summary>
    /// 选中方案变化时回填编辑区
    /// </summary>
    /// <param name="value">新选中的方案</param>
    partial void OnSelectedPlanChanged(TestPlanDto? value)
    {
        if (value != null)
        {
            ApplyPlan(value);
        }
    }

    /// <summary>
    /// 页大小变化后重置到第一页（分页命令可用性由特性联动通知）
    /// </summary>
    /// <param name="value">新的页大小</param>
    partial void OnPageSizeChanged(int value)
    {
        PageIndex = 1;
    }

    #endregion

    #region 私有辅助方法

    /// <summary>
    /// 跳转到指定页并重新查询
    /// </summary>
    /// <param name="pageIndex">目标页码（从 1 开始）</param>
    private void NavigateToPage(int pageIndex)
    {
        if (pageIndex < 1 || pageIndex > TotalPages)
        {
            return;
        }

        PageIndex = pageIndex;
        _ = OnQueryAsync();
    }

    /// <summary>
    /// 初始化 19 个默认激光检测参数项（含 MES 代码与取值方式）
    /// </summary>
    private void InitializeDefaultParameters()
    {
        Parameters = new ObservableCollection<TestParameterDto>(
            LaserDetectorConfigDefaults.CreateDefaultItems().ConvertAll(ToParameterDto));
    }

    /// <summary>
    /// 将配置项转换为检测参数 DTO
    /// <para>按取值方式生成子参数：Range 用 VALUE/LOWER_LIMIT/UPPER_LIMIT，
    /// Value 与 TestMode 用 VALUE，Status 无子参数。</para>
    /// </summary>
    /// <param name="item">激光检测配置项</param>
    /// <returns>检测参数 DTO</returns>
    private static TestParameterDto ToParameterDto(LaserDetectorConfigItem item)
    {
        var dto = new TestParameterDto
        {
            Name = item.Name,
            Code = item.Code,
            MesCode = item.MesCode,
            IsEnabled = item.IsEnabled,
            Order = 0
        };

        switch (item.Kind)
        {
            case LaserDetectorItemKind.Range:
                dto.Parameters.Add(new ParasContentDto
                {
                    Code = SubParameterCodes.Value, Name = SubParameterCodes.ValueDisplayName, Order = SubParameterCodes.ValueOrder
                });
                dto.Parameters.Add(new ParasContentDto
                {
                    Code = SubParameterCodes.LowerLimit, Name = SubParameterCodes.LowerLimitDisplayName, Order = SubParameterCodes.LowerLimitOrder,
                    Value = item.LowerLimit ?? string.Empty, DefaultValue = item.LowerLimit ?? string.Empty
                });
                dto.Parameters.Add(new ParasContentDto
                {
                    Code = SubParameterCodes.UpperLimit, Name = SubParameterCodes.UpperLimitDisplayName, Order = SubParameterCodes.UpperLimitOrder,
                    Value = item.UpperLimit ?? string.Empty, DefaultValue = item.UpperLimit ?? string.Empty
                });
                break;

            case LaserDetectorItemKind.Value:
                dto.Parameters.Add(new ParasContentDto
                {
                    Code = SubParameterCodes.Value, Name = SubParameterCodes.ValueDisplayName, Order = SubParameterCodes.ValueOrder,
                    Value = item.Value ?? string.Empty, DefaultValue = item.Value ?? string.Empty
                });
                break;

            case LaserDetectorItemKind.TestMode:
                dto.Parameters.Add(new ParasContentDto
                {
                    Code = SubParameterCodes.Value, Name = SubParameterCodes.ValueDisplayName, Order = SubParameterCodes.ValueOrder,
                    Value = item.TestMode ?? "01", DefaultValue = item.TestMode ?? "01"
                });
                break;

            case LaserDetectorItemKind.Status:
            default:
                // 状态型检测项仅有启用开关，无子参数。
                break;
        }

        return dto;
    }

    /// <summary>
    /// 按产品编码分页查询方案列表
    /// </summary>
    /// <param name="notifyEmpty">
    /// 是否在查询结果为空时提示用户；查询命令触发时为 true（帮助用户区分
    /// “无匹配方案”与“查询未执行”），翻页与进入页面时为 false（避免干扰）。
    /// </param>
    private async Task OnQueryAsync(bool notifyEmpty = false)
    {
        var result = await _testPlanService.GetBoardConfigsAsync(ProductCode, PageIndex, PageSize, ProductCategory.GasDG);
        Plans = new ObservableCollection<TestPlanDto>(result.Items);
        TotalCount = result.TotalCount;
        PageIndex = result.PageIndex;

        if (notifyEmpty && TotalCount == 0)
        {
            _messageService.GrowlInfo(string.IsNullOrWhiteSpace(ProductCode)
                ? "未查询到任何方案记录！"
                : $"未查询到产品编码包含 [{ProductCode}] 的方案！");
        }
    }

    /// <summary>
    /// 将方案 DTO 回填到编辑区
    /// <para>参数列表先过滤超时参数项（该值为全局项，不参与 19 个检测项的界面陈列，
    /// 保持 XAML 按下标绑定不错位、启用项计数不虚增），超时秒数单独回填编辑框。</para>
    /// </summary>
    /// <param name="plan">测试方案</param>
    private void ApplyPlan(TestPlanDto plan)
    {
        ProductCode = plan.ProductCode;
        StationCount = plan.StationCount;
        // 超时时间从参数项解析（旧方案无该项时回退默认 120 秒）
        MaxTestTimeout = LaserDetectorTimeoutParameter.Resolve(plan.Parameters);
        // 过滤超时参数项，保持编辑区仅 19 个检测项
        Parameters = new ObservableCollection<TestParameterDto>(
            plan.Parameters.Where(p => p.Code != LaserDetectorItemCodes.MaxTestTimeout));
    }

    #endregion

    #region 导航感知实现

    /// <inheritdoc/>
    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    /// <inheritdoc/>
    public void OnNavigatedFrom(NavigationContext navigationContext) { }

    /// <inheritdoc/>
    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        // 读取主界面传入的软件物料号，用于面包屑显示
        var materialNumber = navigationContext.Parameters.GetValue<string>("MaterialNumber");
        if (!string.IsNullOrEmpty(materialNumber))
        {
            CurrentMaterialNumber = materialNumber;
        }

        // 重置到第一页并查询（导航进入不弹空结果提示）
        PageIndex = 1;
        _ = OnQueryAsync();
    }

    #endregion
}
