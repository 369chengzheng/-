// =============================================================================
// 文件名：LoginQueryView.xaml.cs
// 功能描述：登录日志查询视图的后台代码。与 LoginQueryView.xaml 配套，作为 View
//           的 partial 类，负责初始化 XAML 组件，业务逻辑由对应的 LoginQueryViewModel
//           通过数据绑定驱动。
// =============================================================================
using System.Windows.Controls;

namespace IMDP.Presentation.Views;

/// <summary>
/// 登录日志查询视图（4.15 模块）。
/// <para>以分页表格形式展示用户登录记录，支持按工号精确匹配 + 登录时间范围查询。</para>
/// </summary>
public partial class LoginQueryView : UserControl
{
    /// <summary>
    /// 初始化 <see cref="LoginQueryView"/> 类的新实例，并加载 XAML 组件。
    /// </summary>
    public LoginQueryView()
    {
        InitializeComponent();
    }
}
