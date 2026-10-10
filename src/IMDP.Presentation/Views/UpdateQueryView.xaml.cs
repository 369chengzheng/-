// =============================================================================
// 文件名：UpdateQueryView.xaml.cs
// 功能描述：升级日志查询视图的后台代码。与 UpdateQueryView.xaml 配套，作为 View
//           的 partial 类，负责初始化 XAML 组件，业务逻辑由对应的 UpdateQueryViewModel
//           通过数据绑定驱动。
// =============================================================================
using System.Windows.Controls;

namespace IMDP.Presentation.Views;

/// <summary>
/// 升级日志查询视图（4.16 模块）。
/// <para>以分页表格形式展示应用升级记录，支持按版本号模糊匹配 + 更新内容关键词 + 升级时间范围查询，</para>
/// <para>并提供导出 Excel 功能。</para>
/// </summary>
public partial class UpdateQueryView : UserControl
{
    /// <summary>
    /// 初始化 <see cref="UpdateQueryView"/> 类的新实例，并加载 XAML 组件。
    /// </summary>
    public UpdateQueryView()
    {
        InitializeComponent();
    }
}
