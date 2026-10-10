// =============================================================================
// 文件名：UserManageViewModel.cs
// 功能描述：用户管理视图模型。
//           按工号/姓名/角色分页查询用户列表，支持新增/修改/删除用户，
//           并提供简易文本输入对话框用于交互式录入用户信息。
//           继承 PaginationViewModelBase<UserDto>，复用分页状态与翻页命令，仅实现 RefreshAsync()。
// =============================================================================
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Events;
using IMDP.Application.Services;
using IMDP.Application.Events;
using IMDP.Application.DTOs;
using System.Collections.ObjectModel;
using IMDP.Application.Users;
using IMDP.Domain.Interfaces;
using IMDP.Domain.Users;

namespace IMDP.Presentation.ViewModels;

/// <summary>
/// 用户管理视图模型
/// <para>调用 <see cref="IUserService"/> 分页查询/创建/更新/删除用户；
/// 角色筛选与姓名二次过滤在客户端执行（服务接口仅支持单个关键字）。</para>
/// <para>订阅 <see cref="UserLoggedInEvent"/> 获取当前工号，用于禁止删除自身账号与填充审计字段。</para>
/// </summary>
public partial class UserManageViewModel : PaginationViewModelBase<UserDto>
{
    private readonly IUserService _userService;
    private readonly ICurrentContext _currentContext;

    /// <summary>用户列表（当前页）</summary>
    [ObservableProperty]
    private ObservableCollection<UserDto> _users = new();

    /// <summary>当前选中的用户</summary>
    [ObservableProperty]
    private UserDto? _selectedUser;

    // 查询条件：工号
    [ObservableProperty]
    private string _workNumber = string.Empty;

    // 查询条件：姓名
    [ObservableProperty]
    private string _name = string.Empty;

    // 查询条件：角色筛选（"全部" 表示不筛选）
    [ObservableProperty]
    private string _selectedRole = "全部";

    // 当前登录用户工号（删除时用于禁止删除自己）
    private string _currentWorkNumber = string.Empty;

    /// <summary>角色筛选可选项（含"全部"）</summary>
    public List<string> RoleOptions { get; } = new[] { "全部" }
        .Concat(UserRoles.DisplayNames.Values)
        .ToList();
    /// <summary>查询命令</summary>
    public IAsyncRelayCommand QueryCommand { get; }
    public IAsyncRelayCommand MaintainCommand { get; }
    /// <summary>删除用户命令</summary>
    public IAsyncRelayCommand DeleteCommand { get; }
    /// <summary>密码重置命令（仅重置密码，不修改其他字段）</summary>
    public IAsyncRelayCommand ResetPasswordCommand { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="userService">用户服务</param>
    /// <param name="eventAggregator">Prism 事件聚合器</param>
    private readonly IMessageService _messageService;
    public UserManageViewModel(IUserService userService, IEventAggregator eventAggregator, ICurrentContext currentContext,
        IMessageService messageService)
        : base(eventAggregator)
    {
        _messageService = messageService;
        _userService = userService;
        _currentContext = currentContext;
        QueryCommand = new AsyncRelayCommand(OnQueryAsync);
        MaintainCommand = new AsyncRelayCommand(OnMaintainAsync);
        DeleteCommand = new AsyncRelayCommand(OnDeleteAsync);
        ResetPasswordCommand = new AsyncRelayCommand(OnResetPasswordAsync);

        eventAggregator.GetEvent<UserLoggedInEvent>().Subscribe(
            payload => _currentWorkNumber = payload.WorkNumber,
            ThreadOption.UIThread);
    }


    /// <summary>
    /// 执行查询
    /// <para>服务接口仅支持单个关键字（工号或姓名模糊匹配），优先用工号作为关键字；
    /// 工号与姓名同时填写时，姓名在客户端二次过滤；角色筛选也在客户端执行。</para>
    /// </summary>
    public override async Task RefreshAsync()
    {
        try
        {
            var role = SelectedRole == "全部" ? null : GetRoleCode(SelectedRole);
            var result = await _userService.GetUsersAsync(
                WorkNumber,
                Name,
                role,
                PageIndex,
                PageSize);

            Users = new ObservableCollection<UserDto>(result.Items);
            TotalCount = result.TotalCount;
        }
        catch (UnauthorizedAccessException)
        {
            Users.Clear();
            TotalCount = 0;
            _messageService.ShowWarning("当前角色无权访问用户管理", "权限不足");
        }
        catch (Exception ex)
        {
            _messageService.ShowError($"查询用户失败：{ex.Message}", "错误");
        }
    }

    private async Task OnQueryAsync()
    {
        if (PageIndex != 1)
        {
            PageIndex = 1;
            return;
        }

        await RefreshAsync();
    }

    private async Task OnMaintainAsync()
    {
        var workNumber = ShowInputDialog(
            "维护用户", "请输入工号：", SelectedUser?.WorkNumber ?? string.Empty);
        if (string.IsNullOrWhiteSpace(workNumber)) return;
        workNumber = workNumber.Trim();

        var existing = await _userService.GetUserAsync(workNumber);
        var name = ShowInputDialog(
            existing == null ? "新增用户" : "修改用户",
            "请输入姓名：",
            existing?.Name ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name)) return;

        var role = ShowRoleDialog(
            existing == null ? "新增用户" : "修改用户",
            existing?.Role ?? UserRoleCodes.Operator);
        if (string.IsNullOrWhiteSpace(role)) return;

        string? initialPassword = null;
        if (existing == null)
        {
            initialPassword = ShowInputDialog("新增用户", "请输入初始密码：", string.Empty);
            if (string.IsNullOrWhiteSpace(initialPassword))
            {
                _messageService.ShowWarning("初始密码不能为空", "警告");
                return;
            }
        }

        try
        {
            var ok = await _userService.MaintainUserAsync(new UserDto
            {
                Id = existing?.Id ?? 0,
                WorkNumber = workNumber,
                Name = name.Trim(),
                Role = role.Trim()
            }, initialPassword);

            if (!ok)
            {
                _messageService.ShowWarning("用户维护失败，可能是工号重复、角色不允许变更或目标用户不存在", "警告");
                return;
            }

            _messageService.ShowInfo(existing == null ? "新增用户成功" : "修改用户成功");
            SelectedUser = null;
            await RefreshAsync();
        }
        catch (UnauthorizedAccessException)
        {
            _messageService.ShowWarning("当前角色无权维护用户", "权限不足");
        }
        catch (Exception ex)
        {
            _messageService.ShowError($"用户维护失败：{ex.Message}", "错误");
        }
    }

    /// <summary>
    /// 删除用户处理
    /// <para>校验不能删除当前登录用户；二次确认后调用 <see cref="IUserService.DeleteUserAsync"/>。</para>
    /// </summary>
    private async Task OnDeleteAsync()
    {
        if (SelectedUser == null)
        {
            _messageService.ShowWarning("请先选择要删除的用户", "警告");
            return;
        }

        if (string.Equals(SelectedUser.WorkNumber, _currentWorkNumber, StringComparison.OrdinalIgnoreCase))
        {
            _messageService.ShowWarning("不能删除当前登录用户", "警告");
            return;
        }

        if (!_messageService.ShowConfirm(
            $"确认删除用户 {SelectedUser.WorkNumber} {SelectedUser.Name} 吗？",
            "确认删除")) return;

        try
        {
            var ok = await _userService.DeleteUserAsync(SelectedUser.Id);
            if (!ok)
            {
                _messageService.ShowError("删除失败，不能删除当前用户或最后一个系统管理员", "错误");
                return;
            }

            _messageService.ShowInfo("删除用户成功", "提示");
            SelectedUser = null;
            await RefreshAsync();
        }
        catch (UnauthorizedAccessException)
        {
            _messageService.ShowWarning("当前角色无权删除用户", "权限不足");
        }
        catch (Exception ex)
        {
            _messageService.ShowError($"删除用户失败：{ex.Message}", "错误");
        }
    }

    /// <summary>
    /// 密码重置处理
    /// <para>仅重置选中用户的密码，不修改姓名、角色等其他字段。
    /// 通过输入对话框收集新密码，调用 <see cref="IUserService.UpdateUserAsync"/> 传入 newPassword 完成重置。</para>
    /// </summary>
    private async Task OnResetPasswordAsync()
    {
        if (SelectedUser == null)
        {
            _messageService.ShowWarning("请先选择要重置密码的用户", "警告");
            return;
        }

        var newPassword = ShowInputDialog(
            "密码重置", $"请输入 {SelectedUser.WorkNumber} 的新密码：", string.Empty);
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            _messageService.ShowWarning("密码不能为空", "警告");
            return;
        }

        try
        {
            var ok = await _userService.ResetPasswordAsync(SelectedUser.Id, newPassword);
            _messageService.ShowInfo(ok ? "密码重置成功" : "密码重置失败");
        }
        catch (UnauthorizedAccessException)
        {
            _messageService.ShowWarning("当前角色无权重置密码", "权限不足");
        }
        catch (Exception ex)
        {
            _messageService.ShowError($"密码重置失败：{ex.Message}", "错误");
        }
    }

    private static string? GetRoleCode(string roleNameOrCode)
    {
        return UserRoles.GetCode(roleNameOrCode);
    }

    private static string GetRoleName(string roleCode)
    {
        return UserRoles.GetDisplayName(roleCode);
    }

    private string? ShowRoleDialog(string title, string defaultValue)
    {
        var window = new Window
        {
            Title = title,
            Width = 360,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Owner = System.Windows.Application.Current?.MainWindow
        };

        var stack = new StackPanel { Margin = new Thickness(16) };
        var label = new TextBlock
        {
            Text = "请选择角色：",
            Margin = new Thickness(0, 0, 0, 8)
        };
        var roleNames = _currentContext.Role == UserRoleCodes.Admin
            ? UserRoles.DisplayNames.Values.Where(r => r != UserRoles.GetDisplayName(UserRoleCodes.SystemAdmin))
            : UserRoles.DisplayNames.Values;
        var comboBox = new ComboBox
        {
            ItemsSource = roleNames,
            SelectedItem = GetRoleName(defaultValue)
        };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var okButton = new Button
        {
            Content = "确定",
            Width = 72,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true
        };
        var cancelButton = new Button
        {
            Content = "取消",
            Width = 72,
            IsCancel = true
        };
        okButton.Click += (_, _) => { window.DialogResult = true; window.Close(); };
        buttons.Children.Add(okButton);
        buttons.Children.Add(cancelButton);
        stack.Children.Add(label);
        stack.Children.Add(comboBox);
        stack.Children.Add(buttons);
        window.Content = stack;

        return window.ShowDialog() == true && comboBox.SelectedItem is string roleName
            ? GetRoleCode(roleName)
            : null;
    }

    /// <summary>
    /// 简易文本输入对话框（运行时构造，无需新建 XAML 窗口文件），返回 null 表示取消。
    /// </summary>
    /// <param name="title">窗口标题</param>
    /// <param name="prompt">输入提示文本</param>
    /// <param name="defaultValue">输入框默认值</param>
    /// <returns>用户输入的文本；点击取消返回 null</returns>
    internal static string? ShowInputDialog(string title, string prompt, string defaultValue)
    {
        var window = new Window
        {
            Title = title,
            Width = 360,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Owner = System.Windows.Application.Current?.MainWindow
        };

        var stack = new StackPanel { Margin = new Thickness(16) };
        var label = new TextBlock
        {
            Text = prompt,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        };
        var textBox = new TextBox { Text = defaultValue ?? string.Empty };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var okButton = new Button
        {
            Content = "确定",
            Width = 72,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true
        };
        var cancelButton = new Button
        {
            Content = "取消",
            Width = 72,
            IsCancel = true
        };
        okButton.Click += (_, _) => { window.DialogResult = true; window.Close(); };
        buttons.Children.Add(okButton);
        buttons.Children.Add(cancelButton);
        stack.Children.Add(label);
        stack.Children.Add(textBox);
        stack.Children.Add(buttons);
        window.Content = stack;

        textBox.Focus();
        textBox.SelectAll();
        return window.ShowDialog() == true ? textBox.Text : null;
    }

    /// <inheritdoc/>
    /// <remarks>首次进入时触发一次查询，加载首页数据</remarks>
    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        _ = RefreshAsync();
    }
}
