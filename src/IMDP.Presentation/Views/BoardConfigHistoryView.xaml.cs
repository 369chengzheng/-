// =============================================================================
// 文件名：BoardConfigHistoryView.xaml.cs
// 功能描述：模块检测方案配置历史视图的后台代码
//           与 BoardConfigHistoryView.xaml 配套，作为 View 的 partial 类，
//           负责初始化 XAML 组件，业务逻辑由对应的 ViewModel 通过数据绑定驱动。
// =============================================================================
using System.Windows.Controls;

namespace IMDP.Presentation.Views;

/// <summary>
/// 模块检测方案配置历史视图
/// <para>用于展示模块（板）检测方案的配置变更历史记录，支持分页浏览。</para>
/// </summary>
public partial class BoardConfigHistoryView : UserControl
{
    /// <summary>
    /// 初始化 <see cref="BoardConfigHistoryView"/> 类的新实例，并加载 XAML 组件。
    /// </summary>
    public BoardConfigHistoryView()
    {
        InitializeComponent();
    }
}