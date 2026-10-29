// =============================================================================
// 文件名：MainView.xaml.cs
// 功能描述：主视图的后台代码。与 MainView.xaml 配套，作为 View 的 partial 类，
//           负责初始化 XAML 组件及首页图片轮播，查询业务由对应的 ViewModel 驱动。
// =============================================================================
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using IMDP.Presentation.ViewModels;

namespace IMDP.Presentation.Views;

/// <summary>
/// 主视图。
/// <para>提供制令单号与软件物料号输入框及查询按钮，查询时显示加载动画遮罩。</para>
/// </summary>
public partial class MainView : UserControl
{
    private readonly DispatcherTimer _carouselTimer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromSeconds(5)
    };
    private bool _showGasNetwork = true;

    /// <summary>
    /// 初始化 <see cref="MainView"/> 类的新实例，并加载 XAML 组件。
    /// </summary>
    public MainView()
    {
        InitializeComponent();
        _carouselTimer.Tick += OnCarouselTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => _carouselTimer.Start();

    // Prism 切换页面或退出首页时停止计时，缓存的首页不在后台继续轮播。
    private void OnUnloaded(object sender, RoutedEventArgs e) => _carouselTimer.Stop();

    private async void QueryInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        if (e.IsRepeat || DataContext is not MainViewModel vm || !vm.QueryCommand.CanExecute(null)) return;

        var input = (TextBox)sender;
        // 两种查询任选其一；空制令单回车切到物料号，隐藏物料输入的账号仍走原输入校验。
        if (input == OrderNumberInput && string.IsNullOrWhiteSpace(input.Text) && MaterialNumberInput.IsVisible)
        {
            MaterialNumberInput.Focus();
            MaterialNumberInput.SelectAll();
            return;
        }

        await vm.QueryCommand.ExecuteAsync(null);
        if (input.IsVisible && input.IsEnabled)
        {
            input.Focus();
            input.SelectAll();
        }
    }

    private void OnCarouselTick(object? sender, EventArgs e)
    {
        _showGasNetwork = !_showGasNetwork;
        GasNetworkImage.Visibility = _showGasNetwork ? Visibility.Visible : Visibility.Collapsed;
        CooperationImage.Visibility = _showGasNetwork ? Visibility.Collapsed : Visibility.Visible;
        GasNetworkIndicator.Width = _showGasNetwork ? 24 : 8;
        GasNetworkIndicator.Opacity = _showGasNetwork ? 1 : 0.45;
        CooperationIndicator.Width = _showGasNetwork ? 8 : 24;
        CooperationIndicator.Opacity = _showGasNetwork ? 0.45 : 1;
    }
}
