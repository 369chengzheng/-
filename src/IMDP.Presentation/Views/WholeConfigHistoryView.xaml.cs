// =============================================================================
// 文件名：WholeConfigHistoryView.xaml.cs
// 功能描述：整机检测方案配置历史视图的后台代码。与 WholeConfigHistoryView.xaml
//           配套，作为 View 的 partial 类，负责初始化 XAML 组件。整机检测方案
//           配置历史视图以分页列表形式展示整机检测方案的操作类型、方案类型、
//           产品编号、客户编号、操作时间、操作人等变更记录，
//           业务逻辑由对应的 ViewModel 通过数据绑定驱动。
// =============================================================================
using System.Windows.Controls;

namespace IMDP.Presentation.Views;

/// <summary>
/// 整机检测方案配置历史视图。
/// <para>以分页列表形式展示整机检测方案的配置变更历史记录，</para>
/// <para>支持按操作类型、方案类型、产品编号、客户编号等维度浏览。</para>
/// </summary>
public partial class WholeConfigHistoryView : UserControl
{
    /// <summary>
    /// 初始化 <see cref="WholeConfigHistoryView"/> 类的新实例，并加载 XAML 组件。
    /// </summary>
    public WholeConfigHistoryView()
    {
        InitializeComponent();
    }
}