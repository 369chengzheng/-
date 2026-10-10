// =============================================================================
// 文件名：WholeQueryView.xaml.cs
// 功能描述：整机检测查询视图的后台代码。与 WholeQueryView.xaml 配套，作为 View
//           的 partial 类，负责初始化 XAML 组件。整机检测查询视图以分页表格形式
//           展示整机检测记录，支持按测试类型、制令单号、产品SN、产品编号、客户编号、
//           测试结果、创建人、创建时间等维度浏览，
//           业务逻辑由对应的 ViewModel 通过数据绑定驱动。
// =============================================================================
using System.Windows.Controls;

namespace IMDP.Presentation.Views;

/// <summary>
/// 整机检测查询视图。
/// <para>以分页表格形式展示整机检测记录，支持按测试类型、制令单号、产品SN、</para>
/// <para>产品编号、客户编号、测试结果、创建人、创建时间等维度浏览。</para>
/// </summary>
public partial class WholeQueryView : UserControl
{
    /// <summary>
    /// 初始化 <see cref="WholeQueryView"/> 类的新实例，并加载 XAML 组件。
    /// </summary>
    public WholeQueryView()
    {
        InitializeComponent();
    }
}