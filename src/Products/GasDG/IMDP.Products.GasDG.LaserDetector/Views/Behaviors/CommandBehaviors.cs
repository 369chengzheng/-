// =============================================================================
// 文件名：CommandBehaviors.cs
// 功能描述：视图事件到命令的附加行为（自实现，避免引入 Microsoft.Xaml.Behaviors 包）。
//           LostFocus / DropDownOpened 两个 WPF 事件以附加属性形式桥接到 ViewModel 命令，
//           使 XAML 无需 code-behind 事件转发即可完成绑定式交互（严格 MVVM）。
//           FocusNext 提供扫码枪回车后自动跳转下一工位扫码框的纯视图行为
//           （焦点管理属界面交互，不进入 ViewModel）。
// =============================================================================
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;

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

/// <summary>
/// 回车后自动聚焦下一工位输入框的附加行为（纯视图行为，焦点管理不进入 ViewModel）
/// <para>用法：在工位条码框上设置 behaviors:FocusNext.TargetKey="{Binding Station}"；
/// 扫码枪以回车结尾，键起后在同一 ItemsControl 内收集全部已设置 TargetKey 的
/// TextBox（键 = 工位号），聚焦键大于当前值的最小项并全选文本，
/// 实现 1→2→…→n 工位自动跳转；当前已是最后一个工位时不跳转。</para>
/// </summary>
public static class FocusNext
{
    /// <summary>当前元素的目标键（通常绑工位号，用于排序与下一项定位）</summary>
    public static readonly DependencyProperty TargetKeyProperty =
        DependencyProperty.RegisterAttached(
            "TargetKey",
            typeof(int),
            typeof(FocusNext),
            new PropertyMetadata(0, OnTargetKeyChanged));

    /// <summary>
    /// 获取目标键
    /// </summary>
    /// <param name="obj">目标元素</param>
    /// <returns>目标键值</returns>
    public static int GetTargetKey(DependencyObject obj)
        => (int)obj.GetValue(TargetKeyProperty);

    /// <summary>
    /// 设置目标键
    /// </summary>
    /// <param name="obj">目标元素</param>
    /// <param name="value">目标键值</param>
    public static void SetTargetKey(DependencyObject obj, int value)
        => obj.SetValue(TargetKeyProperty, value);

    /// <summary>
    /// 目标键变化时挂接/卸载键盘预览事件（扫码枪回车经 PreviewKeyDown 捕获）
    /// </summary>
    /// <param name="d">目标元素</param>
    /// <param name="e">属性变化事件</param>
    private static void OnTargetKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
        {
            return;
        }

        // 旧值非默认时视为已挂接，先卸载（附加属性在模板重用时会重新赋值）
        if ((int)e.OldValue != 0)
        {
            textBox.PreviewKeyDown -= OnTextBoxPreviewKeyDown;
        }

        if ((int)e.NewValue != 0)
        {
            textBox.PreviewKeyDown += OnTextBoxPreviewKeyDown;
        }
    }

    /// <summary>
    /// 预览回车键：触发后在本组内定位下一目标并聚焦全选（焦点跳转不改变按键处理，
    /// 回车仍可同时触发既有 KeyBinding 命令）
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">键盘事件参数</param>
    private static void OnTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 仅拦截回车（扫码枪结束符）；系统键与修饰键不处理
        if (e.Key != Key.Enter || sender is not TextBox current)
        {
            return;
        }

        var next = FindNextTextBox(current);
        if (next == null)
        {
            return;
        }

        // 延迟到输入事件处理完成后聚焦，避免与 KeyBinding 命令的焦点操作互扰
        current.Dispatcher.BeginInvoke(() =>
        {
            next.Focus();
            next.SelectAll();
        });
    }

    /// <summary>
    /// 在同容器列表中查找当前目标键之后的最小目标键对应的 TextBox
    /// <para>向上定位到承载全部工位条码框的 ItemsControl，收集其中全部
    /// 已设置 TargetKey 的 TextBox（键 = 工位号），取键大于当前值的最小项；
    /// 当前已是最后一个工位时返回 null（不跳转）。</para>
    /// </summary>
    /// <param name="current">当前触发回车的输入框</param>
    /// <returns>下一个工位的输入框；无下一项时返回 null</returns>
    private static TextBox? FindNextTextBox(TextBox current)
    {
        // 向上找到 ItemsControl（工位列表容器），其内部包含全部工位的条码框
        var presenter = FindAncestor<System.Windows.Controls.ItemsControl>(current);
        if (presenter == null)
        {
            return null;
        }

        int currentKey = GetTargetKey(current);
        return FindDescendants<TextBox>(presenter)
            .Where(textBox => (int)textBox.GetValue(TargetKeyProperty) > currentKey)
            .OrderBy(textBox => (int)textBox.GetValue(TargetKeyProperty))
            .FirstOrDefault();
    }

    /// <summary>
    /// 在依赖对象的视觉树中查找指定类型的全部后代
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="container">搜索起点</param>
    /// <returns>全部匹配的后代集合</returns>
    private static IEnumerable<T> FindDescendants<T>(System.Windows.DependencyObject container)
        where T : System.Windows.DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(container);
        for (int index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(container, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// 在依赖对象的视觉树中向上查找指定类型的最近祖先
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="start">搜索起点</param>
    /// <returns>最近的匹配祖先；未找到时返回 null</returns>
    private static T? FindAncestor<T>(System.Windows.DependencyObject start)
        where T : System.Windows.DependencyObject
    {
        var current = start;
        while (current != null)
        {
            if (current is T match)
            {
                return match;
            }

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }
}
