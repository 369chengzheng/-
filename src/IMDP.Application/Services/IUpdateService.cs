// =============================================================================
// 文件名：IUpdateService.cs
// 功能描述：应用更新服务接口
//           定义应用版本检查、下载、安装及更新记录持久化的契约。
//           实现类为 UpdateService，更新记录持久化到 AppUpdateRecord 表。
//           基于 Autoupdater.NET.Official（AutoUpdater.NET）+ 内网 IIS 静态 HTTP 服务。
// =============================================================================
namespace IMDP.Application.Services;

/// <summary>
/// 应用更新服务接口
/// <para>定义应用版本检查、下载、安装及更新记录持久化的契约。</para>
/// <para>实现类为 <see cref="UpdateService"/>，更新记录持久化到 AppUpdateRecord 表。</para>
/// <para>更新检查/下载/安装基于 AutoUpdater.NET，更新清单由内网 IIS 静态 HTTP 服务提供。</para>
/// </summary>
public interface IUpdateService
{
    /// <summary>更新清单 XML 的 URL（内网 IIS 静态文件地址）</summary>
    string UpdateXmlUrl { get; }

    /// <summary>免更新密钥（用户选择"暂不更新"时需输入此密钥方可跳过本次更新）</summary>
    string SkipUpdateKey { get; }

    /// <summary>
    /// 检查是否有可用更新
    /// <para>从 IIS 下载 version.xml 清单，解析最新版本号、下载地址、更新内容，
    /// 与当前安装版本比较。若服务器版本号更高则返回新版本信息，否则返回 null。</para>
    /// </summary>
    /// <returns>新版本信息；无更新或匹配失败时返回 null</returns>
    Task<VersionInfo?> CheckUpdateAsync();

    /// <summary>
    /// 获取全部版本记录（供版本迭代页展示）
    /// <para>从 IIS 下载 version.xml 清单，解析全部版本条目（根节点最新版与 &lt;history&gt; 容器内历史版本），
    /// 按版本号降序排列。网络异常或清单解析失败时返回空集合，不影响应用正常启动。</para>
    /// </summary>
    /// <returns>按版本号降序的全部版本条目；解析失败返回空集合</returns>
    Task<IReadOnlyList<VersionInfo>> GetVersionHistoryAsync();

    /// <summary>获取当前应用版本号</summary>
    /// <returns>当前版本号字符串</returns>
    string GetCurrentVersion();

    /// <summary>校验免更新密钥是否正确</summary>
    /// <param name="key">用户输入的免更新密钥</param>
    /// <returns>密钥正确返回 true，否则返回 false</returns>
    Task<bool> ValidateSkipUpdateKeyAsync(string key);

    /// <summary>记录更新开始（INSERT 一条进行中状态的更新记录）</summary>
    /// <param name="version">目标版本信息</param>
    /// <param name="currentVersion">当前版本号</param>
    /// <returns>新建更新记录的主键 Id</returns>
    Task<long> RecordUpdateStartAsync(VersionInfo version, string currentVersion);

    /// <summary>
    /// 记录更新完成（UPDATE 更新记录的状态与错误信息）
    /// <para>完成时间规则：成功时不写 CompletedTime（留空，待升级后软件自动重启时回填为本次启动时间，
    /// 语义为"升级真正闭环 = 新版本程序启动成功"）；失败时记录失败时刻。</para>
    /// </summary>
    /// <param name="recordId">更新记录 Id</param>
    /// <param name="success">是否成功</param>
    /// <param name="errorMessage">失败时的错误信息（成功时为 null）</param>
    /// <returns>表示异步操作的任务</returns>
    Task RecordUpdateCompletedAsync(long recordId, bool success, string? errorMessage = null);

    /// <summary>
    /// 启动时回填升级完成时间
    /// <para>升级完成时间语义：升级下载完成后，该机器安装新版并自动重启的本次启动时间。</para>
    /// <para>应用每次启动时调用（升级后 AutoUpdater 重启的新进程会命中）：将本机器名下所有
    /// "完成时间仍为空"的升级记录批量回填为本次启动时间。</para>
    /// <para>仅回填 CompletedTime IS NULL 且 UpdateTime &lt;= 本次启动时间的记录：
    /// 已有完成时间的记录不覆盖；防止误回填晚于本次启动才发起的升级记录。</para>
    /// </summary>
    /// <returns>实际回填的记录条数</returns>
    Task<int> BackfillCompletedTimeOnStartupAsync();

    /// <summary>
    /// 同步更新本地 version.xml 清单文件中的版本信息
    /// <para>更新版本时调用：基底按"服务器优先、本地回退"顺序加载 —— 优先获取更新服务器上的 version.xml，
    /// 服务器没有时读取本地文件，两者均无则全新生成。</para>
    /// <para>若传入的版本号高于基底根版本，自动将原有的最新版本转存至 &lt;history&gt; 节点列表中（追加新版本），
    /// 并将新版本号、更新内容 (changelog) 及发布时间 (releaseTime) 写入根节点；版本不高于基底时不降级覆盖。</para>
    /// </summary>
    /// <param name="version">新版本号（如 1.0.3 或 V1.0.3）</param>
    /// <param name="changelog">更新日志描述</param>
    /// <param name="url">更新包下载地址（可空）</param>
    /// <param name="releaseTime">发布时间字符串（格式如 20260806 093047，可空，默认当前时间）</param>
    /// <param name="mandatory">是否强制更新（默认 false）</param>
    /// <returns>表示异步操作的任务</returns>
    Task UpdateVersionXmlAsync(string version, string changelog, string? url = null, string? releaseTime = null, bool mandatory = false);

    /// <summary>
    /// 将代码硬编码中定义的最新版本与更新日志在软件启动时自动同步写入 version.xml
    /// <para>基底按"服务器优先、本地回退"顺序加载：优先获取更新服务器上的 version.xml，
    /// 服务器没有时回退本地文件，两者均无则全新生成。</para>
    /// <para>若代码中的版本高于基底根节点的版本，将原 XML 根节点版本自动移入 &lt;history&gt; 列表中（追加新版本），
    /// 并将代码里的最新版本号、更新日志写回 root 节点；版本不高于基底时不降级覆盖。</para>
    /// </summary>
    /// <returns>表示异步操作的任务</returns>
    Task SyncCodeVersionToXmlAsync();
}

/// <summary>
/// 版本信息
/// <para>描述一个应用版本的元数据，由 <see cref="IUpdateService.CheckUpdateAsync"/> 返回。</para>
/// <para>字段对应内网 IIS version.xml 清单中的条目。</para>
/// </summary>
public class VersionInfo
{
    /// <summary>版本号（如 V1.0.1）</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>发布日期</summary>
    public DateTime ReleaseDate { get; set; }

    /// <summary>发布说明（更新内容描述）</summary>
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>更新包下载地址</summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>更新包 SHA256 校验值（用于完整性校验，可空）</summary>
    public string? CheckSum { get; set; }
}
