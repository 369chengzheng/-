using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using IMDP.Products.GasDG.AlarmHost.Models;
using IMDP.Products.GasDG.AlarmHost.ViewModels;

namespace IMDP.Products.GasDG.AlarmHost.Views;

public partial class AlarmHostConfigView : UserControl
{
    /// <summary>
    /// 方案表格列宽变化的依赖属性描述器，用于订阅用户调节列宽
    /// </summary>
    private static readonly DependencyPropertyDescriptor ColumnWidthDescriptor =
        DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn))
        ?? throw new InvalidOperationException("无法获取表格列宽属性。");
    /// <summary>
    /// 当前页面已订阅变化通知的配置视图模型
    /// </summary>
    private AlarmHostConfigViewModel? _viewModel;
    /// <summary>
    /// 当前页面已订阅密码变化通知的编辑草稿
    /// </summary>
    private AlarmHostConfigDraft? _draft;
    /// <summary>
    /// 专用网络模板当前显示的密码框，随模板加载和卸载更新
    /// </summary>
    private PasswordBox? _networkPasswordInput;
    /// <summary>
    /// 是否正在将草稿密码同步到 PasswordBox，用于避免回写时重入
    /// </summary>
    private bool _syncingPassword;
    /// <summary>
    /// 当前是否已经订阅方案表格各列的宽度变化事件
    /// </summary>
    private bool _columnWidthsAttached;
    /// <summary>
    /// 是否正在更新方案表格宽度，用于避免布局回调重入
    /// </summary>
    private bool _updatingSchemeWidth;
    /// <summary>
    /// 参数区展开时的行高比例，收起前保存，展开后恢复用户拖动的位置
    /// </summary>
    private GridLength _expandedParameterHeight = new(4, GridUnitType.Star);

    public AlarmHostConfigView()
    {
        InitializeComponent();
        AddPlanColumns();
        DataContextChanged += (_, _) => AttachPassword();
        Loaded += (_, _) =>
        {
            AttachPassword();
            AttachColumnWidths();
        };
        Unloaded += (_, _) =>
        {
            DetachPassword();
            DetachColumnWidths();
        };
    }

    // PasswordBox 没有可绑定的 Password 依赖属性；同步只负责本页展示，不做业务保存。
    private void AttachPassword()
    {
        DetachPassword();
        _viewModel = DataContext as AlarmHostConfigViewModel;
        if (_viewModel != null) _viewModel.PropertyChanged += ViewModelChanged;
        AttachDraft();
        UpdateParameterRow();
        UpdatePlanColumns();
    }

    private void DetachPassword()
    {
        if (_viewModel != null) _viewModel.PropertyChanged -= ViewModelChanged;
        if (_draft != null) _draft.PropertyChanged -= DraftChanged;
        _viewModel = null;
        _draft = null;
    }

    private void ViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AlarmHostConfigViewModel.Draft)) AttachDraft();
        if (e.PropertyName == nameof(AlarmHostConfigViewModel.IsParametersExpanded)) UpdateParameterRow();
        if (e.PropertyName == nameof(AlarmHostConfigViewModel.PlanTypeKey))
        {
            SchemeGrid.UnselectAll();
            SchemeHorizontalScroll.ScrollToLeftEnd();
            ParameterScroll.ScrollToTop();
        }
    }

    private void AttachDraft()
    {
        if (_draft != null) _draft.PropertyChanged -= DraftChanged;
        _draft = _viewModel?.Draft;
        if (_draft != null) _draft.PropertyChanged += DraftChanged;
        SyncPassword();
    }

    private void DraftChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AlarmHostConfigDraft.Items)) SyncPassword();
    }

    private void SyncPassword()
    {
        if (_networkPasswordInput == null) return;
        var password = _draft?.Items[AlarmHostFunctionCatalog.MainNetwork].Values["Password"].Value ?? "";
        if (_networkPasswordInput.Password == password) return;
        _syncingPassword = true;
        try { _networkPasswordInput.Password = password; }
        finally { _syncingPassword = false; }
    }

    /// <summary>
    /// 密码框位于模板名称域，加载后连接现有草稿同步逻辑
    /// </summary>
    private void NetworkPasswordLoaded(object sender, RoutedEventArgs e)
    {
        _networkPasswordInput = (PasswordBox)sender;
        SyncPassword();
    }

    private void NetworkPasswordUnloaded(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(_networkPasswordInput, sender)) _networkPasswordInput = null;
    }

    private void NetworkPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_syncingPassword && _draft != null) _draft.Items[AlarmHostFunctionCatalog.MainNetwork].Values["Password"].Value = ((PasswordBox)sender).Password;
    }

    /// <summary>
    /// 收起时释放参数区空间，展开时恢复原比例，不重置原生分界线的拖动结果
    /// </summary>
    private void UpdateParameterRow()
    {
        if (ParameterRowDefinition == null) return;
        var expanded = _viewModel?.IsParametersExpanded ?? true;
        if (expanded)
        {
            if (ParameterRowDefinition.Height.IsAuto) ParameterRowDefinition.Height = _expandedParameterHeight;
        }
        else
        {
            if (ParameterRowDefinition.Height.IsStar) _expandedParameterHeight = ParameterRowDefinition.Height;
            ParameterRowDefinition.Height = GridLength.Auto;
        }
        ParameterRowDefinition.MinHeight = expanded ? 130 : 0;
    }

    /// <summary>
    /// 固定宽度的查询字段自行换行，窄窗口将操作按钮移至独立一行
    /// </summary>
    private void QueryLayoutSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var stacked = e.NewSize.Width < 1100;
        Grid.SetColumnSpan(QueryFields, stacked ? 2 : 1);
        Grid.SetRow(QueryActions, stacked ? 1 : 0);
        Grid.SetColumn(QueryActions, stacked ? 0 : 1);
        Grid.SetColumnSpan(QueryActions, stacked ? 2 : 1);
        QueryActions.Margin = stacked ? new Thickness(0, 8, 0, 0) : new Thickness(0);
    }

    private void AttachColumnWidths()
    {
        if (_columnWidthsAttached) return;
        foreach (var column in SchemeGrid.Columns) ColumnWidthDescriptor.AddValueChanged(column, SchemeColumnWidthChanged);
        _columnWidthsAttached = true;
        UpdateSchemeWidth();
    }

    private void DetachColumnWidths()
    {
        if (!_columnWidthsAttached) return;
        foreach (var column in SchemeGrid.Columns) ColumnWidthDescriptor.RemoveValueChanged(column, SchemeColumnWidthChanged);
        _columnWidthsAttached = false;
    }

    private void SchemeColumnWidthChanged(object? sender, EventArgs e) => UpdateSchemeWidth();
    private void SchemeScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ViewportWidthChange != 0) UpdateSchemeWidth();
    }

    private void UpdatePlanColumns()
    {
        foreach (var column in SchemeGrid.Columns.OfType<DataGridTextColumn>())
        {
            if (column.Binding is not Binding { Path.Path: var path } || !path.StartsWith("Items[", StringComparison.Ordinal)) continue;
            var key = path[6..^1];
            var definition = AlarmHostFunctionCatalog.Get(key);
            column.Header = definition.DisplayName;
        }
        UpdateSchemeWidth();
    }

    /// <summary>
    /// 为目录新增项补充列表列，保留原有列宽和标识列，后续新增功能无需复制XAML列
    /// </summary>
    private void AddPlanColumns()
    {
        var existing = SchemeGrid.Columns.OfType<DataGridTextColumn>()
            .Select(column => (column.Binding as Binding)?.Path?.Path)
            .Where(path => path?.StartsWith("Items[", StringComparison.Ordinal) == true).ToHashSet();
        var insertAt = 2 + existing.Count;
        foreach (var item in AlarmHostFunctionCatalog.All.Where(item => !existing.Contains($"Items[{item.Key}]")))
            SchemeGrid.Columns.Insert(insertAt++, new DataGridTextColumn
            {
                Header = item.DisplayName, Binding = new Binding($"Items[{item.Key}]"),
                Width = 150, MinWidth = 60, ElementStyle = (Style)FindResource("SchemeCellText")
            });
    }

    private void UpdateSchemeWidth()
    {
        if (_updatingSchemeWidth || SchemeGrid == null || SchemeHorizontalScroll == null) return;
        _updatingSchemeWidth = true;
        try
        {
            // 根据用户调节后的各列像素宽度更新外层范围，空表头和有数据时都能到达末列。
            var columnsWidth = SchemeGrid.Columns.Where(c => c.Visibility == Visibility.Visible).Sum(c => Math.Max(c.MinWidth,
                c.Width.IsAbsolute ? c.Width.Value : c.ActualWidth));
            var width = Math.Max(SchemeHorizontalScroll.ViewportWidth,
                columnsWidth + SystemParameters.VerticalScrollBarWidth + 2);
            if (double.IsNaN(SchemeGrid.Width) || Math.Abs(SchemeGrid.Width - width) > .5) SchemeGrid.Width = width;
        }
        finally { _updatingSchemeWidth = false; }
    }
}
