using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using IMDP.Products.GasDG.AlarmHost.Models;
using IMDP.Products.GasDG.AlarmHost.ViewModels;

namespace IMDP.Products.GasDG.AlarmHost.Views;

public partial class AlarmHostView : UserControl
{
    /// <summary>
    /// 页面可见时共用一个UI时钟，卸载后停止，不留下工位后台计时任务
    /// </summary>
    private readonly DispatcherTimer _elapsedTimer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    /// <summary>
    /// 全部工位忙时保留一次扫码定位需求，空闲后恢复，不打断用户其他操作
    /// </summary>
    private bool _waitingForScanFocus;

    public AlarmHostView()
    {
        InitializeComponent();
        _elapsedTimer.Tick += RefreshElapsed;
    }

    /// <summary>
    /// 可见页面开始刷新耗时，重新加载时不会重复订阅Tick
    /// </summary>
    private void ViewLoaded(object sender, RoutedEventArgs e) => _elapsedTimer.Start();

    /// <summary>
    /// 页面离开可视树后停止UI刷新
    /// </summary>
    private void ViewUnloaded(object sender, RoutedEventArgs e) => _elapsedTimer.Stop();

    /// <summary>
    /// 实际起止时间由工位模型保存，视图只触发显示刷新
    /// </summary>
    private void RefreshElapsed(object? sender, EventArgs e)
    {
        if (DataContext is AlarmHostViewModel vm) vm.RefreshElapsedTimes();
        ResumeScanFocus();
    }

    /// <summary>
    /// 新方案的扫码框生成后定位第一个工位，普通换行和方案折叠不重新抢占焦点
    /// </summary>
    private void ScanInputLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: AlarmHostStation { Number: 1 } } input && input.IsEnabled) input.Focus();
    }

    /// <summary>
    /// 回车交由 VM 确认扫码，本视图只负责将焦点移至返回的工位并选中输入内容
    /// 工位列未虚拟化，对应模板控件已随当前方案生成，不排队延迟焦点操作
    /// </summary>
    private void ScanCodeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        var input = (TextBox)sender;
        var station = (AlarmHostStation)input.DataContext;
        var vm = (AlarmHostViewModel)DataContext;
        e.Handled = true;
        input.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        if (vm.ConfirmScan(station) is { } next)
        {
            var container = (ContentPresenter)StationColumns.ItemContainerGenerator.ContainerFromItem(next);
            var nextInput = (TextBox)StationColumns.ItemTemplate.FindName("ScanCodeInput", container);
            nextInput.Focus();
            nextInput.SelectAll();
        }
        else
        {
            input.SelectAll();
        }
        // 先跳焦再启动设备执行，不等待当前工位完成；异步异常由VM统一处理。
        if (station.IsScanned && vm.ExecuteStationCommand.CanExecute(station))
        {
            vm.ExecuteStationCommand.Execute(station);
            _waitingForScanFocus = !vm.Stations.Any(candidate => candidate.CanScan);
        }
    }

    /// <summary>
    /// 窄窗口将操作按钮移至查询框下方，输入框保持可读宽度并自然换行
    /// </summary>
    private void QueryLayoutSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var stacked = e.NewSize.Width < 1120;
        Grid.SetColumnSpan(QueryFields, stacked ? 2 : 1);
        Grid.SetRow(QueryActions, stacked ? 1 : 0);
        Grid.SetColumn(QueryActions, stacked ? 0 : 1);
        Grid.SetColumnSpan(QueryActions, stacked ? 2 : 1);
        QueryActions.Margin = stacked ? new Thickness(0, 8, 0, 0) : new Thickness(12, 0, 0, 0);
    }

    /// <summary>
    /// 打开连接后将输入定位到首个可扫码工位，不等待一次页面重建
    /// </summary>
    private void PortsButtonClick(object sender, RoutedEventArgs e) => _waitingForScanFocus = true;

    /// <summary>
    /// 展开串口下拉框时复用页面枚举命令，活动端口仍保留在候选列表中
    /// </summary>
    private void PortDropDownOpened(object? sender, EventArgs e)
    {
        if (DataContext is AlarmHostViewModel vm && vm.RefreshPortsCommand.CanExecute()) vm.RefreshPortsCommand.Execute();
    }

    /// <summary>
    /// 只恢复由于工位忙或刚打开连接而等待的扫码焦点，用户改点其他控件时停止恢复
    /// </summary>
    private void ResumeScanFocus()
    {
        if (!_waitingForScanFocus || DataContext is not AlarmHostViewModel vm) return;
        if (Keyboard.FocusedElement is TextBox { IsEnabled: true } ||
            Keyboard.FocusedElement is Button button && button != TogglePortsButton)
        { _waitingForScanFocus = false; return; }
        var station = vm.Stations.FirstOrDefault(candidate => candidate.CanScan);
        if (station == null) return;
        var container = (ContentPresenter)StationColumns.ItemContainerGenerator.ContainerFromItem(station);
        if (container == null) return;
        var input = (TextBox)StationColumns.ItemTemplate.FindName("ScanCodeInput", container);
        if (input.Focus()) { input.SelectAll(); _waitingForScanFocus = false; }
    }
}
