// =============================================================================
// 文件名：CommandBehaviors.cs
// 功能描述：视图事件到命令的附加行为（自实现，避免引入 Microsoft.Xaml.Behaviors 包）。
//           LostFocus / DropDownOpened 两个 WPF 事件以附加属性形式桥接到 ViewModel 命令，
//           使 XAML 无需 code-behind 事件转发即可完成绑定式交互（严格 MVVM）。
// =============================================================================
using System.Windows;
using System.Windows.Controls;

namespace IMDP.Products.GasDG.LaserDetector.Views.Behaviors;

/// <summary>
/// 元素 LostFocus 事件的命令附加行为
/// <para>用法：behaviors:LostFocus.Command="{Binding LoadByCodeCommand}"；
/// 可选 CommandParameter（如条码框传工位状态对象）。</para>
/// </summary>
public static class LostFocus
{
    /// <summary>失去焦点时执行的命令</summary>
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(System.Windows.Input.ICommand),
            typeof(LostFocus),
            new PropertyMetadata(null, OnCommandChanged));

    /// <summary>命令参数（缺省传 null）</summary>
    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.RegisterAttached(
            "CommandParameter",
            typeof(object),
            typeof(LostFocus),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取 LostFocus 命令
    /// </summary>
    /// <param name="obj">目标元素</param>
    /// <returns>命令对象；未设置时返回 null</returns>
    public static System.Windows.Input.ICommand GetCommand(DependencyObject obj)
        => (System.Windows.Input.ICommand)obj.GetValue(CommandProperty);

    /// <summary>
    /// 设置 LostFocus 命令
    /// </summary>
    /// <param name="obj">目标元素</param>
    /// <param name="value">命令对象</param>
    public static void SetCommand(DependencyObject obj, System.Windows.Input.ICommand value)
        => obj.SetValue(CommandProperty, value);

    /// <summary>
    /// 获取命令参数
    /// </summary>
    /// <param name="obj">目标元素</param>
    /// <returns>参数对象；未设置时返回 null</returns>
    public static object GetCommandParameter(DependencyObject obj)
        => obj.GetValue(CommandParameterProperty);

    /// <summary>
    /// 设置命令参数
    /// </summary>
    /// <param name="obj">目标元素</param>
    /// <param name="value">参数对象</param>
    public static void SetCommandParameter(DependencyObject obj, object value)
        => obj.SetValue(CommandParameterProperty, value);

    /// <summary>
    /// 命令变化时挂接/卸载 LostFocus 事件
    /// </summary>
    /// <param name="d">目标元素</param>
    /// <param name="e">属性变化事件</param>
    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        if (e.OldValue != null)
        {
            element.LostFocus -= OnElementLostFocus;
        }

        if (e.NewValue != null)
        {
            element.LostFocus += OnElementLostFocus;
        }
    }

    /// <summary>
    /// 失焦时执行附加命令（带参数则传参）
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">路由事件参数</param>
    private static void OnElementLostFocus(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        var command = GetCommand(element);
        if (command?.CanExecute(GetCommandParameter(element)) == true)
        {
            command.Execute(GetCommandParameter(element));
        }
    }
}

/// <summary>
/// ComboBox DropDownOpened 事件的命令附加行为
/// <para>用法：behaviors:DropDownOpened.Command="{Binding RefreshPortsCommand}"，
/// 用于串口下拉展开时刷新可用串口列表。</para>
/// </summary>
public static class DropDownOpened
{
    /// <summary>下拉展开时执行的命令</summary>
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(System.Windows.Input.ICommand),
            typeof(DropDownOpened),
            new PropertyMetadata(null, OnCommandChanged));

    /// <summary>
    /// 获取 DropDownOpened 命令
    /// </summary>
    /// <param name="obj">目标元素</param>
    /// <returns>命令对象；未设置时返回 null</returns>
    public static System.Windows.Input.ICommand GetCommand(DependencyObject obj)
        => (System.Windows.Input.ICommand)obj.GetValue(CommandProperty);

    /// <summary>
    /// 设置 DropDownOpened 命令
    /// </summary>
    /// <param name="obj">目标元素</param>
    /// <param name="value">命令对象</param>
    public static void SetCommand(DependencyObject obj, System.Windows.Input.ICommand value)
        => obj.SetValue(CommandProperty, value);

    /// <summary>
    /// 命令变化时挂接/卸载 DropDownOpened 事件
    /// </summary>
    /// <param name="d">目标元素</param>
    /// <param name="e">属性变化事件</param>
    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ComboBox element)
        {
            return;
        }

        if (e.OldValue != null)
        {
            element.DropDownOpened -= OnElementDropDownOpened;
        }

        if (e.NewValue != null)
        {
            element.DropDownOpened += OnElementDropDownOpened;
        }
    }

    /// <summary>
    /// 下拉展开时执行附加命令
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private static void OnElementDropDownOpened(object? sender, EventArgs e)
    {
        var element = (ComboBox)sender!;
        var command = GetCommand(element);
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }
}
