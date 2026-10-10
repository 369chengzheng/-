// =============================================================================
// 文件名：MainView.xaml.cs
// 功能描述：主视图的后台代码。与 MainView.xaml 配套，作为 View 的 partial 类，
//           负责初始化 XAML 组件。主视图提供制令单号与软件物料号输入及查询入口，
//           查询过程中显示加载动画遮罩，业务逻辑由对应的 ViewModel 驱动。
// =============================================================================
using System.Windows.Controls;

namespace IMDP.Presentation.Views;

/// <summary>
/// 主视图。
/// <para>提供制令单号与软件物料号输入框及查询按钮，查询时显示加载动画遮罩。</para>
/// </summary>
public partial class MainView : UserControl
{
    /// <summary>
    /// 初始化 <see cref="MainView"/> 类的新实例，并加载 XAML 组件。
    /// </summary>
    public MainView()
    {
        InitializeComponent();
    }
}