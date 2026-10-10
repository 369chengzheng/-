// =============================================================================
// 文件名：OrderSwitchQueryView.xaml.cs
// 功能描述：工单切换日志查询视图的后台代码。与 OrderSwitchQueryView.xaml 配套，作为 View
//           的 partial 类，负责初始化 XAML 组件，业务逻辑由对应的 OrderSwitchQueryViewModel
//           通过数据绑定驱动。
// =============================================================================
using System.Windows.Controls;

namespace IMDP.Presentation.Views;

/// <summary>
/// 工单切换日志查询视图（4.17 模块）。
/// <para>以分页表格形式展示生产工单状态切换记录，支持按制令单号模糊匹配 + 操作员模糊匹配 +</para>
/// <para>操作时间范围查询，并提供按操作时间正序/倒序排序切换。</para>
/// </summary>
public partial class OrderSwitchQueryView : UserControl
{
    /// <summary>
    /// 初始化 <see cref="OrderSwitchQueryView"/> 类的新实例，并加载 XAML 组件。
    /// </summary>
    public OrderSwitchQueryView()
    {
        InitializeComponent();
    }
}
