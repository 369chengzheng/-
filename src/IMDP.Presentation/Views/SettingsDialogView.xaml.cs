// =============================================================================
// 文件名：SettingsDialogView.xaml.cs
// 功能描述：全局设置对话框的后台代码。与 SettingsDialogView.xaml 配套，作为 View
//           的 partial 类，负责初始化 XAML 组件。全局设置对话框提供串口参数配置
//           （波特率/数据位/停止位/校验位）与协议命令列表管理（新增/上下移/删除），
//           业务逻辑由对应的 ViewModel 通过数据绑定驱动。
// =============================================================================
using System.Windows;

namespace IMDP.Presentation.Views;

/// <summary>
/// 全局设置对话框。
/// <para>提供串口参数配置（波特率、数据位、停止位、校验位）及协议命令列表的</para>
/// <para>增删改与排序操作，供系统管理员进行通信参数与协议命令维护。</para>
/// </summary>
public partial class SettingsDialogView : Window
{
    /// <summary>
    /// 初始化 <see cref="SettingsDialogView"/> 类的新实例，并加载 XAML 组件。
    /// </summary>
    public SettingsDialogView()
    {
        InitializeComponent();
    }
}