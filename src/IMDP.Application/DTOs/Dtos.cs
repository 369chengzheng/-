// =============================================================================
// 文件名：Dtos.cs
// 功能描述：应用层 DTO 集合，定义跨层（Application ↔ Infrastructure ↔ Presentation）传递的传输数据对象。
//           包含测试参数、检测计划、检测记录、工位、用户、物料产品映射、产品插件等 DTO 定义。
// =============================================================================
using IMDP.Application.Users;
using IMDP.Domain.Products;
using System.Text.Json;

namespace IMDP.Application.DTOs;

/// <summary>
/// 检测项目子参数内容
/// <para>用于表示一个检测项目（<see cref="TestParameterDto"/>）下属的单个子参数，
/// 承载子参数的标识、显示名、取值与排序信息。</para>
/// <para>当前实现中，子参数主要有两种用途：</para>
/// <para>1. 作为"值容器"承载主值/上下限：Code 取常量 <c>VALUE</c>/<c>LOWER_LIMIT</c>/<c>UPPER_LIMIT</c>，
/// Value 存数值字符串（如 "3.6"），由上层按需解析为 double；</para>
/// <para>2. 作为真正语义上的子参数：Code 取子参数变量名（如 READING_DAY），与父参数变量名（如 READING_TYPE）配合使用。</para>
/// <para>常量字符串定义参见 <see cref="SubParameterCodes"/>（本命名空间下）。</para>
/// </summary>

/// <summary>
/// 子参数控件类型
/// <para>指定子参数在视图层（View）中绑定显示的具体控件类型。</para>
/// <para>由 <see cref="ParasContentDto.ControlType"/> 引用，驱动 View 层动态选择编辑控件。</para>
/// </summary>
public enum ParameterControlType
{
    /// <summary>文本输入框（默认控件，适用于字符串、数值等通用输入）</summary>
    TextBox,

    /// <summary>下拉选择框（适用于子参数值为枚举类型时，选项由上层 <c>Options</c> 提供）</summary>
    ComboBox,

    /// <summary>复选框（适用于布尔值类型）</summary>
    CheckBox,
    NumericUpDown
}

public class ParasContentDto
{
    /// <summary>
    /// 子参数编码（Code）
    /// <para>用于唯一标识一个子参数，通常为该子参数的变量名，用于与 View 进行绑定。</para>
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 子参数值（Value）
    /// <para>表示该子参数的具体数值，即该子参数变量所存储的值。</para>
    /// <para>统一以字符串存储，由上层按需解析为 double、整数或其他类型。</para>
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 子参数排序序号（Order）
    /// <para>用于确定该子参数在所属检测项目的所有子参数中的显示顺序。</para>
    /// <para>从 1 开始递增</para>
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 子参数显示名称（Name）
    /// <para>代表该子参数的名称，例如在 View 中该子参数的显示名称。</para>
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 子参数控件类型（ControlType）
    /// <para>指定该子参数在视图层（View）中绑定显示的具体控件类型，如 ComboBox（枚举值）、TextBox 等。</para>
    /// </summary>
    public ParameterControlType ControlType { get; set; } = ParameterControlType.TextBox;

    /// <summary>
    /// 子参数默认值（DefaultValue）
    /// <para>用于存储并设置子参数的初始默认值。对ComboBox控件为枚举键值对</para>
    /// <para>加载方案或重置参数时，将 <see cref="Value"/> 重置为此默认值。</para>
    /// <para>统一以字符串存储，与 <see cref="Value"/> 类型保持一致。</para>
    /// </summary>
    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>
    /// 子参数值校验规则（Validator）
    /// <para>定义对 <see cref="Value"/> 进行有效性验证的规则和逻辑。</para>
    /// <para>为 null 表示不进行额外校验（仅依赖上下限子参数的范围约束）。</para>
    /// <para>校验规则类型详见 <see cref="ParameterValidator"/>。</para>
    /// </summary>
    public ParameterValidator? Validator { get; set; }
}

/// <summary>
/// 子参数值校验规则
/// <para>定义对 <see cref="ParasContentDto.Value"/> 进行有效性验证的规则。</para>
/// <para>支持数值范围校验、正则表达式校验、必填校验、长度校验等多种规则组合。</para>
/// <para>与"值容器"场景下的上下限子参数（LOWER_LIMIT/UPPER_LIMIT）互补：
/// 上下限子参数用于 UI 展示独立的范围输入框，而 Validator 用于对主值进行程序化校验。</para>
/// </summary>
public class ParameterValidator
{
    /// <summary>最小值（数值范围校验下界，为 null 表示不限制下界）</summary>
    public double? MinValue { get; set; }

    /// <summary>最大值（数值范围校验上界，为 null 表示不限制上界）</summary>
    public double? MaxValue { get; set; }

    /// <summary>正则表达式模式（字符串格式校验，为 null 表示不进行正则校验）</summary>
    public string? RegexPattern { get; set; }

    /// <summary>是否必填（true 表示 <see cref="ParasContentDto.Value"/> 不能为空）</summary>
    public bool IsRequired { get; set; }

    /// <summary>最大长度（字符串长度校验，为 null 表示不限制长度）</summary>
    public int? MaxLength { get; set; }

    /// <summary>校验失败时的错误提示信息（为 null 时使用默认错误提示）</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 检测项目参数 DTO
/// <para>对应一个独立的检测项目（如"抄表时间"、"阀门状态"等），承载项目元数据与子参数列表。</para>
/// <para>序列化为 JSON 后存入 <see cref="TestPlanDto.Parameters"/> 字段，由协议层解析下发到表具。</para>
/// </summary>
public class TestParameterDto
{
    /// <summary>检测项目显示名称（如"抄表时间"）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>MES 系统对应的参数编码（用于与上游 MES 系统对接）</summary>
    public string MesCode { get; set; } = string.Empty;

    /// <summary>内部参数编码（用于协议层匹配数据标识）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>是否启用该项目（false 表示该检测项目不参与本次测试）</summary>
    public bool IsEnabled { get; set; }

    /// <summary>子参数列表，详见 <see cref="ParasContentDto"/></summary>
    public List<ParasContentDto> Parameters { get; set; } = new();

    /// <summary>项目排序序号（决定 UI 显示顺序与下发顺序）</summary>
    public int Order { get; set; }
}

/// <summary>
/// 通用分页结果 DTO
/// <para>所有分页查询接口的统一返回类型，承载当前页数据与分页元信息。</para>
/// </summary>
/// <typeparam name="T">分页数据项的类型</typeparam>
public class PagedResultDto<T>
{
    /// <summary>当前页数据列表</summary>
    public List<T> Items { get; set; } = new();

    /// <summary>符合条件的总记录数（用于计算总页数）</summary>
    public int TotalCount { get; set; }

    /// <summary>当前页码（从 1 开始）</summary>
    public int PageIndex { get; set; }

    /// <summary>每页条数</summary>
    public int PageSize { get; set; }
}

/// <summary>
/// 用户信息 DTO
/// <para>用于用户管理界面展示与跨层传递，不包含密码及密码哈希等敏感字段。</para>
/// </summary>
public class UserDto
{
    /// <summary>用户主键 Id</summary>
    public long Id { get; set; }

    /// <summary>工号（登录账号，唯一）</summary>
    public string WorkNumber { get; set; } = string.Empty;

    /// <summary>用户姓名</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>角色（如 Admin/Operator）</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// 映射中文名
    /// </summary>
    public string RoleDisplayName => UserRoles.GetDisplayName(Role);

    /// <summary>创建人（操作员工号）</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>最后更新人</summary>
    public string UpdatedBy { get; set; } = string.Empty;

    /// <summary>最后更新时间</summary>
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 物料-产品插件映射 DTO
/// <para>对应 <see cref="IMDP.Domain.Entities.MaterialProductMap"/> 实体的传输对象。</para>
/// <para>用于建立物料号与一组产品插件 ProductKey 之间的绑定关系，驱动菜单与功能模块加载。</para>
/// </summary>
public class MaterialProductMapDto
{
    /// <summary>主键 Id</summary>
    public long Id { get; set; }

    /// <summary>物料号（业务唯一键）</summary>
    public string MaterialNumber { get; set; } = string.Empty;

    /// <summary>绑定的产品插件 ProductKey 列表的 JSON 序列化字符串</summary>
    public string ProductPluginsJson { get; set; } = string.Empty;

    /// <summary>产品插件显示文本（格式"显示名（ProductKey）"，多项以"、"分隔，供列表列显示）</summary>
    public string ProductPluginsDisplay { get; set; } = string.Empty;

    /// <summary>产品插件悬浮提示文本（每行一项"显示名（ProductKey）"，供 Tooltip 显示被截断的完整列表）</summary>
    public string ProductPluginsToolTip { get; set; } = string.Empty;

    /// <summary>创建人</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>最后更新人</summary>
    public string UpdatedBy { get; set; } = string.Empty;

    /// <summary>最后更新时间</summary>
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 版本记录 DTO
/// <para>用于应用更新模块展示版本信息（版本号、发布日期、发布说明）。</para>
/// </summary>
public class VersionRecordDto
{
    /// <summary>版本号（如 V1.0.1）</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>发布日期</summary>
    public DateTime ReleaseDate { get; set; }

    /// <summary>发布说明（更新内容描述）</summary>
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>更新包下载地址（由服务器 version.xml 提供，供 AutoUpdater 下载）</summary>
    public string DownloadUrl { get; set; } = string.Empty;
}

/// <summary>
/// 检测计划 DTO
/// <para>同时承载板卡配置（BoardConfig）与整机配置（WholeConfig）两种计划类型的数据。</para>
/// <para>整机配置通过 <see cref="PlanType"/> 区分 WholeSet（整机设定）/ WholeGet（整机读取）。</para>
/// <para>历史查询时通过 <see cref="OperationType"/> 标识 INSERT/UPDATE/DELETE 操作类型。</para>
/// </summary>
public class TestPlanDto
{
    /// <summary>计划主键 Id</summary>
    public long Id { get; set; }

    /// <summary>产品编码</summary>
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>客户编码（仅整机配置使用）</summary>
    public string CustomerCode { get; set; } = string.Empty;

    /// <summary>物料号</summary>
    public string MaterialNumber { get; set; } = string.Empty;

    /// <summary>
    /// 产品类别（用于服务层按产品路由到对应仓储，如 GasDG/GasOG/WaterDW/WaterOW）
    /// <para>由产品插件 ViewModel 在保存时填充对应 ProductCategory 常量；</para>
    /// <para>跨产品查询场景（如 WholeConfigHistoryViewModel）可为空。</para>
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>计划类型（整机配置：WholeSet/WholeGet 对应的整数值；板卡配置不使用）</summary>
    public int PlanType { get; set; }

    /// <summary>工位数量</summary>
    public int StationCount { get; set; }

    /// <summary>测试参数列表，详见 <see cref="TestParameterDto"/></summary>
    public List<TestParameterDto> Parameters { get; set; } = new();

    /// <summary>创建人（操作员工号）</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>最后更新人</summary>
    public string UpdatedBy { get; set; } = string.Empty;

    /// <summary>最后更新时间</summary>
    public DateTime UpdatedTime { get; set; }

    /// <summary>操作类型（INSERT/UPDATE/DELETE，仅在历史记录查询时填充）</summary>
    public string OperationType { get; set; } = string.Empty;
}

/// <summary>
/// 检测记录 DTO
/// <para>同时承载板卡检测记录（Board）与整机检测记录（Whole）两种类型的查询与保存数据。</para>
/// <para>整机记录通过 <see cref="TestType"/> 区分 WholeSet/WholeGet。</para>
/// </summary>
public class TestRecordDto
{
    /// <summary>记录主键 Id</summary>
    public long Id { get; set; }

    /// <summary>订单号</summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>产品序列号（产品 SN）</summary>
    public string ProductSn { get; set; } = string.Empty;

    /// <summary>产品编码</summary>
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>客户编码（仅整机记录使用）</summary>
    public string CustomerCode { get; set; } = string.Empty;

    /// <summary>物料号</summary>
    public string MaterialNumber { get; set; } = string.Empty;

    /// <summary>产品类别（来自物料映射，用于区分产品大类）</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>检测类型（整机记录：WholeSet/WholeGet 对应的整数值；板卡记录不使用）</summary>
    public int TestType { get; set; }

    /// <summary>检测结果（true=合格，false=不合格）</summary>
    public bool TestResult { get; set; }

    /// <summary>检测内容 JSON（包含各检测项目的参数与实测值）</summary>
    public string TestContentJson { get; set; } = string.Empty;

    /// <summary>创建人（操作员工号，对应审计字段 UserName）</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>创建时间（即检测发生时间）</summary>
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 工位状态 DTO
/// <para>表示一个检测工位的运行时状态，由 <see cref="IMDP.Application.Services.IStationService"/> 维护。</para>
/// <para>工位数据仅在内存中维护（ObservableCollection），不持久化到数据库。</para>
/// </summary>
public class StationDto
{
    /// <summary>工位 Id（内存自增，从 1 开始）</summary>
    public int Id { get; set; }

    /// <summary>工位名称（如"工位1"）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>绑定的串口名称（如 COM3）</summary>
    public string SerialPort { get; set; } = string.Empty;

    /// <summary>工位状态（Idle=空闲, Testing=测试中, Completed=已完成）</summary>
    public string Status { get; set; } = "Idle";

    /// <summary>当前工位上检测的产品序列号（空闲时为空）</summary>
    public string ProductSn { get; set; } = string.Empty;

    /// <summary>参数值字典（键为参数编码，值为实测值字符串）</summary>
    public Dictionary<string, string> ParameterValues { get; set; } = new();
}

/// <summary>
/// 产品插件信息 DTO
/// <para>用于 UI 层展示已注册的产品插件清单，由 <see cref="IMDP.Application.Services.IProductPluginRegistry"/> 输出。</para>
/// </summary>
public class ProductPluginDto
{
    /// <summary>产品标识（产品插件唯一键，如 GasDG.ProductA）</summary>
    public string ProductKey { get; set; } = string.Empty;

    /// <summary>产品类别（如 GasDG/BoardGW）</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>产品显示名称（用于菜单与界面展示）</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>插件声明的功能菜单标题，统一由插件名与功能后缀生成</summary>
    public List<string> MenuItems { get; set; } = new();
}

/// <summary>
/// 登录记录 DTO
/// <para>对应 <see cref="IMDP.Domain.Entities.Login"/> 实体，用于登录日志查询界面展示。</para>
/// <para>严格参考数据库表 LoginRecords 字段设计：工位、工号、IP 地址、登录类型、登录结果、失败原因、软件版本号、登录时间、登出时间、机器名、系统用户名、应用名称。</para>
/// </summary>
public class LoginRecordDto
{
    /// <summary>记录主键 Id</summary>
    public long Id { get; set; }

    /// <summary>登录工位名称</summary>
    public string Station { get; set; } = string.Empty;

    /// <summary>登录工号</summary>
    public string WorkNumber { get; set; } = string.Empty;

    /// <summary>登录客户端 IP 地址</summary>
    public string? IpAddress { get; set; }

    /// <summary>登录类型/鉴权渠道（MES/Offline，旧数据可空）</summary>
    public string? LoginType { get; set; }

    /// <summary>登录结果（"成功"/"失败"，旧数据可空）</summary>
    public string? LoginResult { get; set; }

    /// <summary>失败原因（登录失败时记录 MES 错误信息或异常信息）</summary>
    public string? ErrorInfo { get; set; }

    /// <summary>登录时客户端软件版本号</summary>
    public string SoftVer { get; set; } = string.Empty;

    /// <summary>登录时间</summary>
    public DateTime LoginTime { get; set; }

    /// <summary>登出时间（可空，用户登出时回填）</summary>
    public DateTime? LogoutTime { get; set; }
}

/// <summary>
/// 应用升级记录 DTO
/// <para>对应 <see cref="IMDP.Domain.Entities.AppUpdateRecord"/> 实体，用于升级日志查询界面展示。</para>
/// <para>严格参考数据库表 AppUpdateRecords 字段设计：升级前版本号、升级后版本号、更新内容、升级开始时间、
/// 升级完成时间、错误信息、升级状态、机器名、系统用户名、应用名称。</para>
/// </summary>
public class AppUpdateRecordDto
{
    /// <summary>记录主键 Id</summary>
    public long Id { get; set; }

    /// <summary>升级前版本号（当前版本）</summary>
    public string CurrentVersion { get; set; } = string.Empty;

    /// <summary>升级后版本号（目标版本）</summary>
    public string TargetVersion { get; set; } = string.Empty;

    /// <summary>更新内容说明（发布说明）</summary>
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>升级状态（Pending/InProgress/Completed/Failed）</summary>
    public string UpdateStatus { get; set; } = string.Empty;

    /// <summary>升级开始时间（可空）</summary>
    public DateTime? UpdateTime { get; set; }

    /// <summary>升级完成时间（可空；成功记录为升级后软件自动重启的本次启动时间，失败记录为失败时刻）</summary>
    public DateTime? CompletedTime { get; set; }

    /// <summary>升级失败时的错误信息（可空）</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>机器名（SqlSugar AOP 自动填充）</summary>
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>系统当前登录用户名（SqlSugar AOP 自动填充）</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>应用名称（SqlSugar AOP 自动填充，可空）</summary>
    public string? AppName { get; set; }
}

/// <summary>
/// 工单切换记录 DTO
/// <para>对应 <see cref="IMDP.Domain.Entities.OrderSwitch"/> 实体，用于工单切换日志查询界面展示。</para>
/// <para>严格参考数据库表 OrderSwitchs 字段设计：制令单号、软件物料号、加载菜单、操作员、操作时间、
/// 机器名、系统用户名、应用名称。</para>
/// </summary>
public class OrderSwitchRecordDto
{
    /// <summary>记录主键 Id</summary>
    public long Id { get; set; }

    /// <summary>制令单号</summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>软件物料号</summary>
    public string MaterialNumber { get; set; } = string.Empty;

    /// <summary>加载菜单 JSON 字符串数组；新记录为标题，历史记录可能为功能枚举名称</summary>
    public string LoadedMenusJson { get; set; } = "[]";

    /// <summary>
    /// 加载菜单的中文友好显示
    /// <para>历史枚举名称转换为通用中文，新记录标题保持原值；JSON 无效时回退显示原始内容</para>
    /// </summary>
    public string LoadedMenusDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(LoadedMenusJson))
                return string.Empty;

            try
            {
                var menuNames = JsonSerializer.Deserialize<List<string>>(LoadedMenusJson);
                if (menuNames is null || menuNames.Count == 0)
                    return string.Empty;

                return string.Join("、", menuNames.Select(menuName =>
                    Enum.TryParse<FunctionModuleType>(menuName, true, out var module)
                        ? module.GetDisplayName()
                        : menuName));
            }
            catch (JsonException)
            {
                return LoadedMenusJson;
            }
        }
    }

    /// <summary>操作员（创建人工号）</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>操作时间（创建时间）</summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>机器名（SqlSugar AOP 自动填充）</summary>
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>系统当前登录用户名（SqlSugar AOP 自动填充）</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>应用名称（SqlSugar AOP 自动填充，可空）</summary>
    public string? AppName { get; set; }
}
