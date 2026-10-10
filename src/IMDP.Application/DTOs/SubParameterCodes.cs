// =============================================================================
// 文件名：SubParameterCodes.cs
// 功能描述：子参数（ParasContentDto）Code/Name/Order 常量定义。
//           将散落在各产品（GasDG.ProductA / BoardGW.ProductX）ViewModel 中的
//           硬编码字符串 "VALUE" / "LOWER_LIMIT" / "UPPER_LIMIT" 及其显示名、
//           排序序号统一收敛为常量，避免拼写错误、便于全局检索与未来扩展。
//           常量值与历史持久化数据保持完全一致，不影响数据交互。
//           放置于 IMDP.Application.DTOs 命名空间，与 ParasContentDto 同位置，
//           供所有产品共享。
// =============================================================================
namespace IMDP.Application.DTOs;

/// <summary>
/// 子参数 Code/Name/Order 常量
/// <para>用于 <see cref="ParasContentDto"/> 的 <c>Code</c>、<c>Name</c>、<c>Order</c> 字段，
/// 标识"值容器"场景下子参数在所属检测项目中的角色（主值/下限/上限）。</para>
/// <para>常量值与历史持久化 JSON 数据中的字符串严格一致，不得修改。</para>
/// <para>使用方：ConfigParameter / SchemeParameter / WholeConfigViewModel /
/// WholeViewModel / BoardConfigViewModel / BoardViewModel 等。</para>
/// </summary>
public static class SubParameterCodes
{
    /// <summary>
    /// 主值子参数 Code
    /// <para>标识检测项目的主参数值。对应子参数 Name=<see cref="ValueDisplayName"/>，Order=<see cref="ValueOrder"/>。</para>
    /// </summary>
    public const string Value = "VALUE";

    /// <summary>
    /// 下限子参数 Code
    /// <para>标识检测项目的下限值（用于数值范围校验）。对应子参数 Name=<see cref="LowerLimitDisplayName"/>，Order=<see cref="LowerLimitOrder"/>。</para>
    /// </summary>
    public const string LowerLimit = "LOWER_LIMIT";

    /// <summary>
    /// 上限子参数 Code
    /// <para>标识检测项目的上限值（用于数值范围校验）。对应子参数 Name=<see cref="UpperLimitDisplayName"/>，Order=<see cref="UpperLimitOrder"/>。</para>
    /// </summary>
    public const string UpperLimit = "UPPER_LIMIT";

    /// <summary>
    /// 主值子参数显示名（对应 <see cref="ParasContentDto.Name"/>）
    /// </summary>
    public const string ValueDisplayName = "值";

    /// <summary>
    /// 下限子参数显示名（对应 <see cref="ParasContentDto.Name"/>）
    /// </summary>
    public const string LowerLimitDisplayName = "下限";

    /// <summary>
    /// 上限子参数显示名（对应 <see cref="ParasContentDto.Name"/>）
    /// </summary>
    public const string UpperLimitDisplayName = "上限";

    /// <summary>
    /// 主值子参数排序序号（对应 <see cref="ParasContentDto.Order"/>）
    /// </summary>
    public const int ValueOrder = 1;

    /// <summary>
    /// 下限子参数排序序号（对应 <see cref="ParasContentDto.Order"/>）
    /// </summary>
    public const int LowerLimitOrder = 2;

    /// <summary>
    /// 上限子参数排序序号（对应 <see cref="ParasContentDto.Order"/>）
    /// </summary>
    public const int UpperLimitOrder = 3;
}