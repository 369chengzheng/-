// =============================================================================
// 文件名：VersionView.xaml.cs
// 功能描述：版本迭代视图的后台代码。与 VersionView.xaml 配套，作为 View 的
//           partial 类，负责初始化 XAML 组件。版本迭代视图以表格形式展示版本号、
//           发布日期与更新内容，业务逻辑由对应的 ViewModel 通过数据绑定驱动。
// =============================================================================
using System.Windows.Controls;

namespace IMDP.Presentation.Views;

/// <summary>
/// 版本迭代视图。
/// <para>以表格形式展示版本号、发布日期与更新内容（多行显示），</para>
/// <para>按发布日期倒序排列，供用户查看版本迭代记录。</para>
/// </summary>
public partial class VersionView : UserControl
{
    /// <summary>
    /// 初始化 <see cref="VersionView"/> 类的新实例，并加载 XAML 组件。
    /// </summary>
    public VersionView()
    {
        InitializeComponent();
    }
}