// =============================================================================
// 文件名：StationColumnControl.xaml.cs
// 功能描述：StationColumnControl 的代码后置。
//           处理产品 SN 输入框的回车事件，并以事件形式向宿主视图通知扫码内容。
// =============================================================================
using System.Windows.Controls;
using System.Windows.Input;

namespace IMDP.Presentation.Controls;

/// <summary>
/// 工位列控件代码后置
/// <para>负责监听扫码输入框的回车按键，并通过 <see cref="SnEntered"/> 事件向宿主视图传递扫码内容。</para>
/// </summary>
public partial class StationColumnControl : UserControl
{
    /// <summary>
    /// 构造函数，初始化组件
    /// </summary>
    public StationColumnControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 产品 SN 扫码完成事件
    /// <para>当用户在 SN 输入框按下回车时触发，事件参数为输入框文本内容。</para>
    /// </summary>
    public event EventHandler<string>? SnEntered;

    /// <summary>
    /// SN 输入框按键事件处理
    /// <para>按下回车时触发 <see cref="SnEntered"/> 事件，传递当前输入框文本。</para>
    /// </summary>
    /// <param name="sender">事件源（TextBox）</param>
    /// <param name="e">按键事件参数</param>
    private void OnSnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox textBox)
        {
            SnEntered?.Invoke(this, textBox.Text);
        }
    }
}