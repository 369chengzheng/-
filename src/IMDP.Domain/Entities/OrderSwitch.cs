// =============================================================================
// 文件名：OrderSwitch.cs
// 功能描述：订单切换记录实体，对应数据库 OrderSwitchs 表。
//           记录订单切换时实际加载的菜单项快照，用于日志查询。
// =============================================================================
using SqlSugar;

namespace IMDP.Domain.Entities;

/// <summary>
/// 订单切换记录实体
/// <para>对应数据库表 OrderSwitchs，在订单切换时记录该订单关联的菜单项列表。</para>
/// <para>LoadedMenusJson 以 JSON 字符串数组形式保存当时的菜单快照，供日志查询展示。</para>
/// <para>实现 <see cref="IAuditable"/>，由 SqlSugar AOP 自动填充审计字段。</para>
/// </summary>
[SugarTable("OrderSwitchs")]
public class OrderSwitch : IAuditable
{
    /// <summary>主键 Id（BIGINT IDENTITY 自增主键）</summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    /// <summary>订单号（长度 100）</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>物料编号（长度 100）</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string MaterialNumber { get; set; } = string.Empty;

    /// <summary>已加载菜单项 JSON 数组（默认空数组，NVARCHAR(MAX)）</summary>
    [SugarColumn(ColumnDataType = "NVARCHAR(MAX)", IsNullable = false)]
    public string LoadedMenusJson { get; set; } = "[]";

    /// <summary>创建人（操作员工号，由应用层填充，长度 50）</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>机器名（由 SqlSugar AOP 自动填充本机 NetBIOS 名，长度 128）</summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>系统当前登录用户名（由 SqlSugar AOP 自动填充，长度 128）</summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>应用名称（由 SqlSugar AOP 自动填充，可空，长度 128）</summary>
    [SugarColumn(Length = 128, IsNullable = true)]
    public string? AppName { get; set; }
}
