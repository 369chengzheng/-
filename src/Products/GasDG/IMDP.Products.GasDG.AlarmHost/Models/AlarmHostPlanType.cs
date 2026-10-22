namespace IMDP.Products.GasDG.AlarmHost.Models;

/// <summary>
/// 导航转换及边界校验；直接沿用平台 int 类型的 0=设置、1=检验约定
/// </summary>
public static class AlarmHostPlanTypes
{
    public static int? FromNavigation(string? key) => key?.Trim() switch
    {
        "WholeSet" => 0,
        "WholeGet" => 1,
        _ => null
    };

    public static bool IsValid(int type) => type is 0 or 1;
}
