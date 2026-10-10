// =============================================================================
// 文件名：BoardQueryView.xaml.cs
// 功能描述：模块检测查询视图的后台代码。与 BoardQueryView.xaml 配套，作为 View
//           的 partial 类，负责初始化 XAML 组件，业务逻辑由对应的 ViewModel
//           通过数据绑定驱动。
// =============================================================================
using System.Windows.Controls;

namespace IMDP.Presentation.Views;

/// <summary>
/// 模块检测查询视图。
/// <para>以分页表格形式展示模块（板）检测记录，支持按制令单号、产品SN、产品编号、</para>
/// <para>测试结果、创建人、创建时间等维度浏览。</para>
/// </summary>
public partial class BoardQueryView : UserControl
{
    /// <summary>
    /// 初始化 <see cref="BoardQueryView"/> 类的新实例，并加载 XAML 组件。
    /// </summary>
    public BoardQueryView()
    {
        InitializeComponent();
    }
}
