// =============================================================================
// 文件名：UserService.cs
// 功能描述：用户服务实现，提供用户登录验证、增删改查及密码管理。
//           密码采用 PBKDF2 加盐哈希存储（PasswordHasher）；删除为软删除（置 IsDeleted = true）。
//           向后兼容：登录时识别旧 SHA256 哈希，验证通过后自动升级为 PBKDF2。
// =============================================================================
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using IMDP.Application.DTOs;
using IMDP.Domain.Entities;
using IMDP.Domain.Interfaces;
using IMDP.Domain.Services;
using IMDP.Application.Users;
using IMDP.Application.Events;
using Prism.Events;
using IMDP.Domain.Users;

namespace IMDP.Application.Services;

/// <summary>
/// 用户服务实现
/// <para>实现 <see cref="IUserService"/>，依赖 <see cref="IUserRepository"/> 读写 User 表。</para>
/// <para>密码安全：使用 <see cref="PasswordHasher"/>（PBKDF2-HMAC-SHA256 加盐），
/// 登录时若检测到旧 SHA256 哈希则验证通过后自动重新哈希为 PBKDF2 格式。</para>
/// <para>删除策略：软删除（置 <see cref="User.IsDeleted"/> = true），不物理移除记录。</para>
/// </summary>
public class UserService : IUserService
{
    /// <summary>用户仓储，用于读写 User 表</summary>
    private readonly IUserRepository _userRepository;
    private readonly ILoginConfigRepository _loginConfigRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IEventAggregator _eventAggregator;
    private readonly IMesService _mesService;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="userRepository">用户仓储（由 DI 注入）</param>
    /// <param name="loginConfigRepository">登录配置日志仓储（写入 LoginConfig 表，由 DI 注入）</param>
    /// <param name="currentContext">当前上下文（SqlSugar AOP 审计字段填充用，由 DI 注入）</param>
    /// <param name="eventAggregator">事件聚合器（发布登录成功事件，由 DI 注入）</param>
    /// <param name="mesService">MES 服务（用于登录鉴权，由 DI 注入）</param>
    public UserService(IUserRepository userRepository, ILoginConfigRepository loginConfigRepository, ICurrentContext currentContext, IEventAggregator eventAggregator, IMesService mesService)
    {
        _userRepository = userRepository;
        _loginConfigRepository = loginConfigRepository;
        _currentContext = currentContext;
        _eventAggregator = eventAggregator;
        _mesService = mesService;
    }

    /// <inheritdoc/>
    public async Task<bool> LoginAsync(string workNumber, string password)
    {
        var user = await _userRepository.GetByWorkNumberAsync(workNumber.Trim());
        if (user == null || !PasswordHasher.VerifyPassword(password, user.PasswordHash))
            return false;

        if (PasswordHasher.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = PasswordHasher.HashPassword(password);
            user.UpdatedTime = DateTime.Now;
            await _userRepository.UpdateAsync(user);
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> ValidatePasswordAsync(string workNumber, string password)
    {
        var user = await _userRepository.GetByWorkNumberAsync(workNumber.Trim());
        if (user == null) return false;

        return PasswordHasher.VerifyPassword(password, user.PasswordHash);
    }

    /// <inheritdoc/>
    public async Task<MesCheckUserResult> LoginViaMesAsync(string workNumber, string password, string resCode)
    {
        // 登录鉴权改走 MES：调用 BenQGuru eMES 的 CheckUserInfo 校验工号/密码/资源编码，
        // 不再查询本地 Users 表进行密码校验。
        return await _mesService.CheckUserInfoAsync(workNumber.Trim(), password, resCode.Trim());
    }

    /// <inheritdoc/>
    public async Task<long> RecordLoginAsync(string workNumber, string station, string loginType, bool isSuccess, string? errorInfo)
    {
        try
        {
            var loginTime = DateTime.Now;
            var softVer = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? string.Empty;
            var ipAddress = GetLocalIpv4();
            var loginResult = isSuccess ? "成功" : "失败";
            // LoginConfig.ErrorInfo 列长 500，超长错误信息截断，避免插入失败导致整条登录日志丢失
            var errorInfoValue = isSuccess || string.IsNullOrEmpty(errorInfo)
                ? null
                : (errorInfo.Length > 500 ? errorInfo.Substring(0, 500) : errorInfo);


            // 写入 LoginConfig 表（登录日志，登录成功与失败均记录）
            // 使用返回自增主键的插入方法，供登出时回填登出时间
            var loginConfig = new LoginConfig
            {
                Station = station,
                WorkNumber = workNumber,
                IpAddress = ipAddress,
                LoginType = loginType,
                LoginResult = loginResult,
                ErrorInfo = errorInfoValue,
                SoftVer = softVer,
                LoginTime = loginTime
                // ComputerName/UserName/AppName 由 SqlSugar AOP 自动填充
            };
            var loginRecordId = await _loginConfigRepository.InsertReturnIdentityAsync(loginConfig);

            // 返回刚写入的登录记录主键 Id，供登出时回填登出时间
            return loginRecordId;
        }
        catch
        {
            // 登录日志写入失败不应阻断登录流程，静默忽略
            return 0;
        }
    }

    /// <inheritdoc/>
    public async Task RecordLogoutAsync(long loginRecordId)
    {
        if (loginRecordId <= 0) return;

        try
        {
            // 按 Id 取回本次登录写入的 LoginConfig 记录，回填登出时间（仅当尚未回填时）
            var loginConfig = await _loginConfigRepository.GetByIdAsync(loginRecordId);
            if (loginConfig != null && loginConfig.LogoutTime == null)
            {
                loginConfig.LogoutTime = DateTime.Now;
                await _loginConfigRepository.UpdateAsync(loginConfig);
            }
        }
        catch
        {
            // 登出时间回填失败不应阻断登出流程，静默忽略
        }
    }

    /// <summary>
    /// 获取本机 IPv4 地址
    /// <para>取 DNS 解析本机主机名后的首个 IPv4 地址；获取失败返回 null（数据库列可空）。</para>
    /// </summary>
    private static string? GetLocalIpv4()
    {
        try
        {
            return Dns.GetHostEntry(Dns.GetHostName())
                .AddressList
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                ?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<UserDto?> GetUserAsync(string workNumber)
    {
        var user = await _userRepository.GetByWorkNumberAsync(workNumber.Trim());
        if (user == null) return null;

        return MapToDto(user);
    }

    public async Task<PagedResultDto<UserDto>> GetUsersAsync(
        string? workNumberKeyword,
        string? nameKeyword,
        string? role,
        int pageIndex,
        int pageSize)
    {
        await EnsureCanManageUsersAsync();

        var normalizedRole = string.IsNullOrWhiteSpace(role) ? null : NormalizeRole(role);
        if (!string.IsNullOrWhiteSpace(role) && normalizedRole == null)
            return new PagedResultDto<UserDto> { PageIndex = pageIndex, PageSize = pageSize };

        var result = await _userRepository.GetPagedAsync(
            workNumberKeyword?.Trim(),
            nameKeyword?.Trim(),
            normalizedRole,
            Math.Max(1, pageIndex),
            Math.Max(1, pageSize));

        return new PagedResultDto<UserDto>
        {
            Items = result.Items.Select(MapToDto).ToList(),
            TotalCount = result.TotalCount,
            PageIndex = result.PageIndex,
            PageSize = result.PageSize
        };
    }

    public async Task<bool> MaintainUserAsync(UserDto user, string? initialPassword = null)
    {
        var actorRole = await EnsureCanManageUsersAsync();

        var workNumber = user.WorkNumber.Trim();
        var name = user.Name.Trim();
        var role = NormalizeRole(user.Role);
        if (string.IsNullOrEmpty(workNumber) || string.IsNullOrEmpty(name) || role == null)
            return false;

        var existing = await _userRepository.GetByWorkNumberAsync(workNumber);
        if (existing != null)
            return await UpdateEntityAsync(existing, name, role, null, actorRole);

        if (actorRole == UserRoleCodes.Admin && role == UserRoleCodes.SystemAdmin)
            return false;
        if (string.IsNullOrWhiteSpace(initialPassword) ||
            await _userRepository.WorkNumberExistsAsync(workNumber, null))
            return false;

        var now = DateTime.Now;
        await _userRepository.AddAsync(new User
        {
            WorkNumber = workNumber,
            Name = name,
            Role = role,
            PasswordHash = PasswordHasher.HashPassword(initialPassword),
            CreatedBy = _currentContext.UserName,
            CreatedTime = now,
            UpdatedBy = _currentContext.UserName,
            UpdatedTime = now
        });
        return true;
    }

    public async Task<bool> CreateUserAsync(UserDto user, string password)
    {
        var actorRole = await EnsureCanManageUsersAsync();

        var workNumber = user.WorkNumber.Trim();
        var name = user.Name.Trim();
        var role = NormalizeRole(user.Role);
        if (string.IsNullOrEmpty(workNumber) || string.IsNullOrEmpty(name) ||
            role == null || string.IsNullOrWhiteSpace(password) ||
            await _userRepository.WorkNumberExistsAsync(workNumber, null))
            return false;

        if (actorRole == UserRoleCodes.Admin && role == UserRoleCodes.SystemAdmin)
            return false;

        var now = DateTime.Now;
        await _userRepository.AddAsync(new User
        {
            WorkNumber = workNumber,
            Name = name,
            Role = role,
            PasswordHash = PasswordHasher.HashPassword(password),
            CreatedBy = _currentContext.UserName,
            CreatedTime = now,
            UpdatedBy = _currentContext.UserName,
            UpdatedTime = now
        });
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateUserAsync(UserDto user, string? newPassword = null)
    {
        var actorRole = await EnsureCanManageUsersAsync();

        var entity = await _userRepository.GetByIdAsync(user.Id);
        var role = NormalizeRole(user.Role);
        if (entity == null || entity.IsDeleted || string.IsNullOrWhiteSpace(user.Name) || role == null)
            return false;

        return await UpdateEntityAsync(entity, user.Name.Trim(), role, newPassword, actorRole);
    }

    public async Task<bool> ResetPasswordAsync(long id, string newPassword)
    {
        var actorRole = await EnsureCanManageUsersAsync();
        if (string.IsNullOrWhiteSpace(newPassword)) return false;

        var entity = await _userRepository.GetByIdAsync(id);
        if (entity == null || entity.IsDeleted) return false;
        if (actorRole == UserRoleCodes.Admin && entity.Role == UserRoleCodes.SystemAdmin)
            return false;

        entity.PasswordHash = PasswordHasher.HashPassword(newPassword);
        entity.UpdatedBy = _currentContext.UserName;
        entity.UpdatedTime = DateTime.Now;
        await _userRepository.UpdateAsync(entity);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteUserAsync(long id)
    {
        var actorRole = await EnsureCanManageUsersAsync();

        var entity = await _userRepository.GetByIdAsync(id);
        if (entity == null || entity.IsDeleted) return false;
        if (string.Equals(entity.WorkNumber, _currentContext.UserName, StringComparison.OrdinalIgnoreCase))
            return false;
        if (actorRole == UserRoleCodes.Admin && entity.Role == UserRoleCodes.SystemAdmin)
            return false;
        if (entity.Role == UserRoleCodes.SystemAdmin &&
            await _userRepository.CountActiveByRoleAsync(UserRoleCodes.SystemAdmin) <= 1)
            return false;

        // 软删除：仅标记 IsDeleted，不物理移除
        entity.IsDeleted = true;
        entity.UpdatedBy = _currentContext.UserName;
        entity.UpdatedTime = DateTime.Now;
        await _userRepository.UpdateAsync(entity);
        return true;
    }

    private async Task<bool> UpdateEntityAsync(User entity, string name, string role, string? newPassword, string actorRole)
    {
        if (actorRole == UserRoleCodes.Admin)
        {
            if (entity.Role == UserRoleCodes.SystemAdmin || role == UserRoleCodes.SystemAdmin)
                return false;
        }
        if (entity.Role == UserRoleCodes.SystemAdmin && role != UserRoleCodes.SystemAdmin &&
            await _userRepository.CountActiveByRoleAsync(UserRoleCodes.SystemAdmin) <= 1)
            return false;

        entity.Name = name;
        entity.Role = role;
        entity.UpdatedBy = _currentContext.UserName;
        entity.UpdatedTime = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(newPassword))
            entity.PasswordHash = PasswordHasher.HashPassword(newPassword);

        await _userRepository.UpdateAsync(entity);

        // 修改的是当前登录用户时，同步更新上下文中缓存的角色，避免后续操作使用过时权限
        if (string.Equals(entity.WorkNumber, _currentContext.UserName, StringComparison.OrdinalIgnoreCase))
        {
            _currentContext.SetUser(entity.WorkNumber, role);
            _eventAggregator.GetEvent<UserRoleChangedEvent>().Publish(new UserRoleChangedPayload
            {
                WorkNumber = entity.WorkNumber,
                New_Role = role
            });
        }

        return true;
    }

    private async Task<string> EnsureCanManageUsersAsync()
    {
        var currentUser = await _userRepository.GetByWorkNumberAsync(_currentContext.UserName);
        if (currentUser?.Role is not (UserRoleCodes.SystemAdmin or UserRoleCodes.Admin))
            throw new UnauthorizedAccessException("Current user cannot manage users.");

        return currentUser.Role;
    }

    private static string? NormalizeRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role)) return null;
        return UserRoles.GetCode(role.Trim());
    }
    /// <summary>
    /// 将 User 实体映射为 UserDto
    /// </summary>
    /// <param name="user">用户实体</param>
    /// <returns>用户 DTO（不含密码相关字段）</returns>
    private static UserDto MapToDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            WorkNumber = user.WorkNumber,
            Name = user.Name,
            Role = user.Role,
            CreatedBy = user.CreatedBy,
            CreatedTime = user.CreatedTime,
            UpdatedBy = user.UpdatedBy,
            UpdatedTime = user.UpdatedTime
        };
    }
}
