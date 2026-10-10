// =============================================================================
// 文件名：LoginView.xaml.cs
// 功能描述：登录视图的后台代码。与 LoginView.xaml 配套，作为 View 的 partial 类，
//           负责初始化 XAML 组件，业务逻辑（工位/工号/密码校验、离线登录、登录跳转）
//           由对应的 ViewModel 通过数据绑定驱动。
// =============================================================================
using System.Windows.Controls;

namespace IMDP.Presentation.Views;

/// <summary>
/// 登录视图。
/// <para>提供工位、工号、密码输入及离线登录选项，用于用户身份认证。</para>
/// </summary>
public partial class LoginView : UserControl
{
    /// <summary>
    /// 初始化 <see cref="LoginView"/> 类的新实例，并加载 XAML 组件。
    /// </summary>
    public LoginView()
    {
        InitializeComponent();
    }
}