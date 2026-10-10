// =============================================================================
// 文件名：UserManageView.xaml.cs
// 功能描述：用户管理视图的后台代码。与 UserManageView.xaml 配套，作为 View 的
//           partial 类，负责初始化 XAML 组件。用户管理视图提供按工号/姓名/角色
//           查询、新增、修改、删除用户等功能，业务逻辑由对应的 ViewModel
//           通过数据绑定驱动。
// =============================================================================
using System.Windows.Controls;

namespace IMDP.Presentation.Views;

/// <summary>
/// 用户管理视图。
/// <para>提供按工号、姓名、角色查询用户，以及新增、修改、删除用户等功能，</para>
/// <para>仅系统管理员和管理员可见。</para>
/// </summary>
public partial class UserManageView : UserControl
{
    /// <summary>
    /// 初始化 <see cref="UserManageView"/> 类的新实例，并加载 XAML 组件。
    /// </summary>
    public UserManageView()
    {
        InitializeComponent();
    }
}