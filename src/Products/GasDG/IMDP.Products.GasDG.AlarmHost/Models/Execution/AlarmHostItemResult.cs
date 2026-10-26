namespace IMDP.Products.GasDG.AlarmHost.Models;

/// <summary>
/// 单个项目实际执行结果，显示值与MES纯值分开；MES码沿用所属方案行
/// </summary>
/// <param name="Code">目录中的稳定功能编码</param>
/// <param name="Value">结果表显示值，多值采用英文逗号连接</param>
/// <param name="MesValue">供后续MES上传使用的实际参数值，不带显示标签</param>
/// <param name="IsPass">读取格式或目标核对是否通过，不包含MES及数据库结果</param>
/// <param name="Message">失败原因或本项成功说明</param>
public sealed record AlarmHostItemResult(string Code, string Value, string MesValue, bool IsPass, string Message);
