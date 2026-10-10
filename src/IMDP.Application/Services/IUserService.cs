// =============================================================================
// 文件名：IUserService.cs
// 功能描述：用户服务接口，定义用户登录验证、查询、增删改及密码校验契约。
//           UserDto 与 PagedResultDto<T> 统一使用 IMDP.Application.DTOs 命名空间下的定义。
// =============================================================================
using IMDP.Application.DTOs;
using IMDP.Domain.Interfaces;
using IMDP.Domain.Products;

namespace IMDP.Application.Services;

/// <summary>
/// 用户服务接口
/// <para>封装用户登录验证、用户管理（增删改查）与密码校验逻辑。</para>
/// <para>实现类为 <see cref="UserService"/>，密码采用 SHA256 哈希存储。</para>
/// </summary>
public interface IUserService
{
    /// <summary>用户登录验证</summary>
    /// <param name="workNumber">工号</param>
    /// <param name="password">明文密码</param>
    /// <returns>验证通过返回 true；用户不存在或密码错误返回 false</returns>
    Task<bool> LoginAsync(string workNumber, string password);

    /// <summary>按工号获取用户信息</summary>
    /// <param name="workNumber">工号</param>
    /// <returns>用户 DTO；不存在返回 null</returns>
    Task<UserDto?> GetUserAsync(string workNumber);

    /// <summary>
    /// 通过 MES 校验用户登录（密码校验走 MES，不再查询本地 Users 表）。
    /// <para>对应 AutoFrock 的 MESMethod.CheckUserInfo；登录鉴权改走 MES。
    /// 校验通过后，调用方应再调用 <see cref="GetUserAsync"/> 从本地表读取角色/姓名用于权限映射（不比对密码）。</para>
    /// </summary>
    /// <param name="workNumber">工号</param>
    /// <param name="password">明文密码</param>
    /// <param name="resCode">资源编码（通常由工位号映射）</param>
    /// <returns>MES 校验结果；Success=true 表示登录允许</returns>
    Task<MesCheckUserResult> LoginViaMesAsync(string workNumber, string password, string resCode);

    /// <summary>
    /// 记录一条登录日志（登录成功与失败均调用）。
    /// <para>写入 LoginRecords 表：登录工位、工号、本机 IP 地址、登录类型（鉴权渠道）、登录结果、
    /// 失败原因、软件版本号、登录时间；机器名/用户名/应用名由 SqlSugar AOP 自动填充。
    /// 写入失败仅吞掉异常，不阻断登录流程。</para>
    /// </summary>
    /// <param name="workNumber">登录工号</param>
    /// <param name="station">登录工位</param>
    /// <param name="loginType">登录类型/鉴权渠道（见 <see cref="IMDP.Domain.Users.LoginTypeCodes"/>）</param>
    /// <param name="isSuccess">是否登录成功</param>
    /// <param name="errorInfo">失败原因（成功时传 null）</param>
    /// <returns>刚写入的 LoginRecords 记录主键 Id（写入失败返回 0），供登出时回填登出时间</returns>
    Task<long> RecordLoginAsync(string workNumber, string station, string loginType, bool isSuccess, string? errorInfo);

    /// <summary>
    /// 正常退出登录时回填登出时间。
    /// <para>按登录记录 Id 更新 LoginRecords 表中对应记录的 LogoutTime 为当前时间；
    /// 记录不存在或已回填则忽略。写入失败仅吞掉异常，不阻断登出流程。</para>
    /// </summary>
    /// <param name="loginRecordId">登录成功时写入的 LoginRecords 记录主键 Id</param>
    Task RecordLogoutAsync(long loginRecordId);

    /// <summary>分页查询用户列表</summary>
    /// <param name="keyword">关键字（模糊匹配工号或姓名，为空则不筛选）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <returns>分页结果</returns>
    Task<PagedResultDto<UserDto>> GetUsersAsync(string? workNumberKeyword, string? nameKeyword, string? role, int pageIndex, int pageSize);

    Task<bool> MaintainUserAsync(UserDto user, string? initialPassword = null);

    /// <summary>创建用户</summary>
    /// <param name="user">用户信息 DTO</param>
    /// <param name="password">明文密码（将哈希后存储）</param>
    /// <returns>是否创建成功（工号已存在返回 false）</returns>
    Task<bool> CreateUserAsync(UserDto user, string password);

    /// <summary>更新用户信息（可选重置密码）</summary>
    /// <param name="user">用户信息 DTO</param>
    /// <param name="newPassword">新密码（为 null 或空则不修改密码）</param>
    /// <returns>是否更新成功（用户不存在返回 false）</returns>
    Task<bool> UpdateUserAsync(UserDto user, string? newPassword = null);

    Task<bool> ResetPasswordAsync(long id, string newPassword);

    /// <summary>删除用户（软删除，置 IsDeleted = true）</summary>
    /// <param name="id">用户 Id</param>
    /// <returns>是否删除成功（用户不存在返回 false）</returns>
    Task<bool> DeleteUserAsync(long id);

    /// <summary>校验密码（不修改登录状态，仅验证）</summary>
    /// <param name="workNumber">工号</param>
    /// <param name="password">明文密码</param>
    /// <returns>验证通过返回 true；用户不存在或密码错误返回 false</returns>
    Task<bool> ValidatePasswordAsync(string workNumber, string password);
}