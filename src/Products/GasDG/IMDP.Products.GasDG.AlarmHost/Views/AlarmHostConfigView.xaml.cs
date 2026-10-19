using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using IMDP.Products.GasDG.AlarmHost.Models;
using IMDP.Products.GasDG.AlarmHost.ViewModels;

namespace IMDP.Products.GasDG.AlarmHost.Views;

public partial class AlarmHostConfigView : UserControl
{
    private static readonly DependencyPropertyDescriptor ColumnWidthDescriptor =
        DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn))
        ?? throw new InvalidOperationException("无法获取表格列宽属性。");
    private AlarmHostConfigViewModel? _viewModel;
    private AlarmHostConfigDraft? _draft;
    private bool _syncingPassword;
    private bool _columnWidthsAttached;
    private bool _updatingSchemeWidth;

    public AlarmHostConfigView()
    {
        InitializeComponent();
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
        if (e.PropertyName == nameof(AlarmHostConfigViewModel.IsInspectionPlan))
        {
            UpdatePlanColumns();
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
        if (e.PropertyName == nameof(AlarmHostConfigDraft.NetworkPassword)) SyncPassword();
    }

    private void SyncPassword()
    {
        var password = _draft?.NetworkPassword ?? "";
        if (NetworkPasswordInput.Password == password) return;
        _syncingPassword = true;
        try { NetworkPasswordInput.Password = password; }
        finally { _syncingPassword = false; }
    }

    private void NetworkPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_syncingPassword && _draft != null) _draft.NetworkPassword = NetworkPasswordInput.Password;
    }

    private void ParameterScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ViewportWidthChange == 0 || ParameterScroll.ViewportWidth <= 0) return;
        // 必须按受约束的视口计算，而不是按 WrapPanel 自身宽度；后者会被旧 ItemWidth
        // 反向撑大，造成从宽窗口缩窄时复杂行仍停留在旧宽度。
        var width = ParameterScroll.ViewportWidth;
        var columns = Math.Clamp((int)(width / 360), 1, 4);
        SettingRows.ItemWidth = InspectionRows.ItemWidth = Math.Floor(width / columns);
        ComplexRows.ItemWidth = Math.Floor(width / Math.Clamp((int)(width / 720), 1, 2));
    }

    private void UpdateParameterRow()
    {
        if (ParameterRowDefinition == null) return;
        var expanded = _viewModel?.IsParametersExpanded ?? true;
        ParameterRowDefinition.Height = expanded ? new GridLength(4, GridUnitType.Star) : GridLength.Auto;
        ParameterRowDefinition.MinHeight = expanded ? 72 : 0;
    }

    private void QueryLayoutSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 文档宽屏同一行；小窗口下操作移到第二行，避免压窄两个方案编码输入。
        var stacked = e.NewSize.Width < 1000;
        Grid.SetColumnSpan(QueryFields, stacked ? 2 : 1);
        Grid.SetRow(QueryActions, stacked ? 1 : 0);
        Grid.SetColumn(QueryActions, stacked ? 0 : 1);
        Grid.SetColumnSpan(QueryActions, stacked ? 2 : 1);
        QueryActions.Margin = stacked ? new Thickness(0, 8, 0, 0) : new Thickness(0);
    }

    private void QueryFieldsSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 0) QueryFields.ItemWidth = Math.Min(380, Math.Floor(e.NewSize.Width / 2));
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
        var visibility = _viewModel?.IsInspectionPlan == true ? Visibility.Visible : Visibility.Collapsed;
        foreach (var column in new[] { ImeiColumn, ImsiColumn, IccidColumn, CsqColumn,
                     VersionColumn, BluetoothSnOrSparkMacColumn, BluetoothBroadcastColumn })
            column.Visibility = visibility;
        UpdateSchemeWidth();
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
