// =============================================================================
// 文件名：ApplicationEvents.cs
// 功能描述：应用层 Prism 事件集合，定义基于 PubSubEvent 的跨模块松耦合通信事件。
//           涵盖用户登录/登出、物料号变更、菜单刷新、导航、工位列重建等事件及其载荷。
// =============================================================================
using Prism.Events;
using IMDP.Application.DTOs;
using IMDP.Domain.Products;

namespace IMDP.Application.Events;

/// <summary>
/// 用户登录成功事件
/// <para>当用户通过登录验证后触发，载荷为登录用户信息 <see cref="UserLoggedInPayload"/>。</para>
/// <para>订阅者：主界面用于更新当前用户显示、权限按钮可见性等。</para>
/// </summary>
public class UserLoggedInEvent : PubSubEvent<UserLoggedInPayload>
{
}

/// <summary>
/// 用户登录事件载荷
/// <para>携带登录用户的工号、姓名、角色、工位与登录时间，供订阅方更新界面状态。</para>
/// </summary>
public class UserLoggedInPayload
{
    /// <summary>登录用户工号</summary>
    public string WorkNumber { get; set; } = string.Empty;

    /// <summary>登录用户姓名</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>登录用户角色（如 Admin/Operator）</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>登录所在工位名称</summary>
    public string Station { get; set; } = string.Empty;

    /// <summary>登录时间</summary>
    public DateTime LoginTime { get; set; }
}

/// <summary>
/// 用户登出事件
/// <para>当用户主动登出或会话失效时触发，无载荷。</para>
/// <para>订阅者：主界面用于清空当前用户状态、返回登录页。</para>     
/// </summary>
public class UserLoggedOutEvent : PubSubEvent
{
}

/// <summary>
/// 物料号变更事件
/// <para>当全局物料号被修改后触发，载荷为新物料号字符串。</para>
/// <para>订阅者：菜单刷新器根据新物料号重新加载对应的产品插件功能模块。</para>
/// </summary>
public class MaterialNumberChangedEvent : PubSubEvent<string>
{
}

/// <summary>
/// 菜单刷新事件，携带当前物料号对应的产品插件功能模块列表
/// </summary>
public class MenuRefreshedEvent : PubSubEvent<List<FunctionModuleType>>
{
}

/// <summary>
/// 首页一次查询的完整解析结果事件
/// <para>将制令单号、物料号、原始菜单和发起人作为一个不可拆分的载荷发布，避免菜单与物料号分别发布产生时序错配</para>
/// </summary>
public class HomeMenuResolvedEvent : PubSubEvent<HomeMenuResolvedPayload>
{
}

/// <summary>
/// 首页菜单解析结果载荷
/// </summary>
public sealed class HomeMenuResolvedPayload
{
    /// <summary>
    /// 制令单号；手动物料查询时为空
    /// </summary>
    public string OrderNumber { get; init; } = string.Empty;

    /// <summary>
    /// 软件物料号；MES 未取得物料时为空
    /// </summary>
    public string MaterialNumber { get; init; } = string.Empty;

    /// <summary>
    /// 尚未经过角色和工位过滤的原始菜单
    /// </summary>
    public IReadOnlyList<FunctionModuleType> RawModules { get; init; } = Array.Empty<FunctionModuleType>();

    /// <summary>带插件归属及显示标题的当前菜单；新工单切换日志保存其过滤后的标题快照。</summary>
    public IReadOnlyList<ProductMenuItemDto> RawMenuItems { get; init; } = Array.Empty<ProductMenuItemDto>();

    /// <summary>
    /// 发起查询的操作员工号
    /// </summary>
    public string OperatorWorkNumber { get; init; } = string.Empty;

    /// <summary>
    /// 解析完成时间
    /// </summary>
    public DateTime ResolvedTime { get; init; }
}

/// <summary>
/// 请求打开全局设置弹窗事件
/// </summary>
public class OpenSettingsDialogEvent : PubSubEvent
{
}

/// <summary>
/// 请求导航到指定功能模块事件
/// </summary>
public class NavigateToModuleEvent : PubSubEvent<NavigateToModulePayload>
{
}

/// <summary>
/// 模块导航事件载荷
/// <para>指定目标视图名称及可选的 PlanType 参数（用于整机配置区分 WholeSet/WholeGet）。</para>
/// <para>携带当前软件物料号，供目标视图在面包屑导航中显示。</para>
/// </summary>
public class NavigateToModulePayload
{
    /// <summary>目标视图名称（如 WholeConfigView、BoardConfigView）</summary>
    public string ViewName { get; set; } = string.Empty;

    /// <summary>计划类型参数（WholeSet/WholeGet，仅整机配置视图使用；其他为 null）</summary>
    public string? PlanType { get; set; }

    /// <summary>当前软件物料号（由主界面查询时获取，传递给目标视图用于面包屑显示）</summary>
    public string? MaterialNumber { get; set; }
}

/// <summary>
/// 返回首页事件
/// <para>触发主区域导航回首页视图，无载荷。</para>
/// <para>订阅者：主界面 ViewModel 执行区域导航回 Home。</para>
/// </summary>
public class GoHomeEvent : PubSubEvent
{
}

/// <summary>
/// 工位列重建事件
/// <para>当工位数量变化时触发，载荷为新的工位总数。</para>
/// <para>订阅者：主界面工位列控件据此重建列布局。</para>
/// </summary>
public class RebuildStationColumnsEvent : PubSubEvent<int>
{
}

/// <summary>
/// 用户角色变更事件
/// <para>当用户修改自身角色时触发（如系统管理员将自己降级为管理员），
/// 订阅据此刷新菜单权限与状态栏显示。</para>
public class UserRoleChangedEvent : PubSubEvent<UserRoleChangedPayload>
{
}

public class UserRoleChangedPayload
{
    /// <summary>
    /// 角色发生变更的用户工号
    /// </summary>
    public string WorkNumber { get; set; } = string.Empty;
    /// <summary>
    /// 变更后的新角色
    /// </summary>
    public string New_Role { get; set; } = string.Empty;
}
