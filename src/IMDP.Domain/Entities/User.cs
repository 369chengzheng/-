// =============================================================================
// 文件名：User.cs
// 功能描述：用户实体，对应数据库 Users 表。
//           存储系统操作员的工号、姓名、密码哈希与角色等账户信息。
// =============================================================================
using SqlSugar;
using IMDP.Domain.Users;

namespace IMDP.Domain.Entities;

/// <summary>
/// 用户实体
/// <para>对应数据库表 Users，记录可登录系统的操作员账户。</para>
/// <para>PasswordHash 仅存储哈希值，明文密码不落库。</para>
/// <para>实现 <see cref="IAuditable"/>，由 SqlSugar AOP 自动填充审计字段。</para>
/// <para>实现 <see cref="ISoftDeletable"/>，删除时置 IsDeleted=true，不物理删除。</para>
/// </summary>
[SugarTable("Users")]
public class User : IAuditable, ISoftDeletable
{
    /// <summary>主键 Id（BIGINT IDENTITY 自增主键）</summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    /// <summary>工号（登录账号，列名 WorkNumber，长度 50）</summary>
    [SugarColumn(ColumnName = "WorkNumber", Length = 50, IsNullable = false)]
    public string WorkNumber { get; set; } = string.Empty;

    /// <summary>姓名（长度 100）</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>密码哈希值（长度 256，仅存哈希不存明文）</summary>
    [SugarColumn(Length = 256, IsNullable = false)]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>角色（Admin/Engineer/Operator 等，长度 20，默认 Operator）</summary>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string Role { get; set; } = UserRoleCodes.Operator;

    /// <summary>创建人（操作员工号，由应用层填充，长度 50）</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>最后更新人（操作员工号，由应用层填充，长度 50）</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string UpdatedBy { get; set; } = string.Empty;

    /// <summary>最后更新时间</summary>
    public DateTime UpdatedTime { get; set; }

    /// <summary>软删除标记（true 表示已删除）</summary>
    public bool IsDeleted { get; set; }

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