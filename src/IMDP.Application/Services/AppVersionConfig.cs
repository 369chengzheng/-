// =============================================================================
// 文件名：AppVersionConfig.cs
// 功能描述：应用版本与更新日志硬编码定义配置类。
//           由开发者在发布新版本前在代码中维护版本号、更新说明与发布时间。
//           程序启动时会自动读取此类中的配置并同步写入更新至本地 version.xml 文件。
// =============================================================================
namespace IMDP.Application.Services;

/// <summary>
/// 应用当前硬编码版本与更新说明定义
/// <para>开发者在发布新版本前手动修改此代码文件中的 Version、ReleaseNotes 与 ReleaseTime。</para>
/// <para>程序启动后将自动解析此定义并同步写回更新至磁盘的 version.xml 清单中（含历史版本归档）。</para>
/// </summary>
public static class AppVersionConfig
{
    /// <summary>当前应用程序版本号（如 1.0.3）</summary>
    public const string Version = "1.0.1";

    /// <summary>当前版本更新日志与更新说明</summary>
    public const string ReleaseNotes = "• 修复了部分bug";

    /// <summary>当前版本发布时间（格式：yyyy-MM-dd HH:mm:ss 或 yyyyMMdd HHmmss）</summary>
    public const string ReleaseTime = "2026-08-06 10:30:47";

    /// <summary>
    /// 默认更新包下载相对路径（仅存路径部分，不记录 IP 与端口号）
    /// <para>运行时自动从 appsettings.json 的 Update:UpdateXmlUrl 提取"协议://IP:端口"与本路径拼接为完整下载地址。</para>
    /// <para>示例：UpdateXmlUrl = http://192.168.87.94:668/version.xml，本值 = /Package/IMDP_V1.0.1.zip，
    /// 则实际下载地址 = http://192.168.87.94:668/Package/IMDP_V1.0.1.zip。</para>
    /// <para>更换更新服务器时只需修改 appsettings.json，无需改动本常量与重新编译。</para>
    /// </summary>
    public const string DownloadUrl = "/Package/IMDP_V1.0.1.zip";

    /// <summary>是否为强制更新版本</summary>
    public const bool Mandatory = false;
}
