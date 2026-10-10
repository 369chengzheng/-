// =============================================================================
// 文件名：MaterialProductMapView.xaml.cs
// 功能描述：物料产品维护视图的后台代码。与 MaterialProductMapView.xaml 配套，
//           作为 View 的 partial 类，负责初始化 XAML 组件。物料产品维护视图
//           提供软件物料号查询、产品插件配置（按分类分组的复选框）、维护与删除操作，
//           业务逻辑由对应的 ViewModel 通过数据绑定驱动。
// =============================================================================
using System.Windows.Controls;

namespace IMDP.Presentation.Views;

/// <summary>
/// 物料产品维护视图。
/// <para>用于维护软件物料号与产品插件之间的映射关系，支持按分类分组配置插件、</para>
/// <para>查询、维护及删除操作。</para>
/// </summary>
public partial class MaterialProductMapView : UserControl
{
    /// <summary>
    /// 初始化 <see cref="MaterialProductMapView"/> 类的新实例，并加载 XAML 组件。
    /// </summary>
    public MaterialProductMapView()
    {
        InitializeComponent();
    }
}