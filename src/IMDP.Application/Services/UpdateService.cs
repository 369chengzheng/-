// =============================================================================
// 文件名：UpdateService.cs
// 功能描述：应用更新服务实现，提供版本检查、更新记录的持久化。
//           版本检查：HttpClient 下载并解析内网 IIS 上的 version.xml 清单，与当前版本比较。
//           下载安装：由 Presentation 层 UpdateViewModel 调用 AutoUpdater.NET 内置流程完成
//                     （断点续传、SHA256 校验、启动安装包、重启），本层不依赖 AutoUpdater.NET。
//           更新记录：开始/完成时写入 AppUpdateRecord 表（真实数据库操作）。
// =============================================================================
using System.Net;
using System.Xml;
using IMDP.Domain.Entities;
using IMDP.Domain.Interfaces;

namespace IMDP.Application.Services;

/// <summary>
/// 应用更新服务实现
/// <para>实现 <see cref="IUpdateService"/>，依赖 <see cref="IAppUpdateRecordRepository"/> 持久化更新记录。</para>
/// <para>版本检查通过 HttpClient 解析内网 IIS 上的 version.xml 清单；
/// 下载/安装/重启由 Presentation 层委托 AutoUpdater.NET 内置流程完成（含断点续传与 SHA256 完整性校验）。</para>
/// <para>本服务位于应用层，不直接依赖 AutoUpdater.NET（WPF 库），保持层间依赖正确。</para>
/// </summary>
public class UpdateService : IUpdateService
{
    /// <summary>应用更新记录仓储，用于持久化更新过程记录</summary>
    private readonly IAppUpdateRecordRepository _updateRecordRepository;

    /// <summary>更新清单 XML 的 URL（内网 IIS 静态文件地址，由 appsettings.json 注入）</summary>
    private readonly string _updateXmlUrl;

    /// <summary>免更新密钥（由 appsettings.json 注入）</summary>
    private readonly string _skipUpdateKey;

    /// <inheritdoc/>
    public string UpdateXmlUrl => _updateXmlUrl;

    /// <inheritdoc/>
    public string SkipUpdateKey => _skipUpdateKey;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="updateRecordRepository">更新记录仓储（由 DI 注入）</param>
    /// <param name="updateXmlUrl">更新清单 XML 的 URL（由 appsettings.json Update:UpdateXmlUrl 注入）</param>
    /// <param name="skipUpdateKey">免更新密钥（由 appsettings.json Update:SkipUpdateKey 注入）</param>
    private readonly IHttpClientService _httpClient;
    public UpdateService(IAppUpdateRecordRepository updateRecordRepository,
        string updateXmlUrl, string skipUpdateKey, IHttpClientService httpClientService)
    {
        _httpClient = httpClientService;
        _updateRecordRepository = updateRecordRepository;
        _updateXmlUrl = updateXmlUrl ?? string.Empty;
        _skipUpdateKey = skipUpdateKey ?? string.Empty;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 流程：HttpClient GET version.xml → 解析版本号/下载地址/更新内容 → 取清单最新版本 →
    /// 与当前版本比较 → 返回 VersionInfo?。网络异常或清单解析失败时静默返回 null，不影响应用正常启动。
    /// </remarks>
    public async Task<VersionInfo?> CheckUpdateAsync()
    {
        if (string.IsNullOrWhiteSpace(_updateXmlUrl))
            return null;

        try
        {
            var xml = await _httpClient.GetStringAsync(_updateXmlUrl);
            var info = ParseUpdateXml(xml);
            if (info == null)
                return null;

            // 仅当服务器版本号高于当前本地 version.xml 版本才返回
            if (!IsNewerVersion(info.Version, GetLocalVersionFromXml()))
                return null;

            return info;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 流程：HttpClient GET version.xml → 解析全部版本条目（根节点最新版与 &lt;history&gt; 容器内历史版本）→
    /// 按版本号降序排列返回。网络异常或清单解析失败时静默返回空集合，不影响应用。
    /// </remarks>
    public async Task<IReadOnlyList<VersionInfo>> GetVersionHistoryAsync()
    {
        if (string.IsNullOrWhiteSpace(_updateXmlUrl))
            return Array.Empty<VersionInfo>();

        try
        {
            var xml = await _httpClient.GetStringAsync(_updateXmlUrl);
            return ParseUpdateXmlAll(xml);
        }
        catch
        {
            return Array.Empty<VersionInfo>();
        }
    }

    /// <inheritdoc/>
    public string GetCurrentVersion()
    {
        var ver = AppVersionConfig.Version;
        return ver.StartsWith("V", StringComparison.OrdinalIgnoreCase) ? ver : "V" + ver;
    }

    /// <summary>
    /// 从本地根目录下的 version.xml 解析根节点 <version>
    /// <para>如果文件不存在或解析失败，退回到 1.0.0。</para>
    /// </summary>
    /// <returns>解析到的版本号字符串</returns>
    private static string GetLocalVersionFromXml()
    {
        try
        {
            var xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.xml");
            if (File.Exists(xmlPath) && new FileInfo(xmlPath).Length > 0)
            {
                var doc = new XmlDocument();
                doc.Load(xmlPath);
                var verStr = doc.DocumentElement?.SelectSingleNode("version")?.InnerText?.Trim();
                if (!string.IsNullOrEmpty(verStr))
                {
                    return verStr;
                }
            }
        }
        catch
        {
            // 异常或空文件时静默回退
        }
        return "1.0.0";
    }

    /// <inheritdoc/>
    public Task<bool> ValidateSkipUpdateKeyAsync(string key)
    {
        return Task.FromResult(string.Equals(key, _skipUpdateKey, StringComparison.Ordinal));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 流程：采集客户端机器名与 IPv4 地址 → 构造"进行中"状态的记录 →
    /// INSERT 到 AppUpdateRecord 表 → 返回新记录 Id。
    /// </remarks>
    public async Task<long> RecordUpdateStartAsync(VersionInfo version, string currentVersion)
    {
        var record = new AppUpdateRecord
        {
            CurrentVersion = currentVersion,
            TargetVersion = version.Version,
            ReleaseNotes = version.ReleaseNotes,
            ClientMachineName = Environment.MachineName,
            ClientIpAddress = GetLocalIpAddress(),
            UpdateStatus = "进行中",
            UpdateTime = DateTime.Now
        };

        var result = await _updateRecordRepository.AddAsync(record);
        return result.Id;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 流程：按 Id 查询记录 → UPDATE 状态为 已完成/失败、完成时间、错误信息。
    /// 记录不存在时静默返回。
    /// </remarks>
    public async Task RecordUpdateCompletedAsync(long recordId, bool success, string? errorMessage = null)
    {
        var record = await _updateRecordRepository.GetByIdAsync(recordId);
        if (record == null) return;

        record.UpdateStatus = success ? "已完成" : "失败";
        record.CompletedTime = DateTime.Now;
        record.ErrorMessage = errorMessage ?? string.Empty;
        await _updateRecordRepository.UpdateAsync(record);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 解析本地 version.xml，若传入的新版本号高于当前根版本号，将原有的最新版本数据归档放入 &lt;history&gt; 节点列表中，
    /// 并将新版本号、changelog、releaseDate 等覆盖写入根节点并保存至磁盘文件。
    /// </remarks>
    public async Task UpdateVersionXmlAsync(string version, string changelog, string? url = null, string? releaseTime = null, bool mandatory = false)
    {
        if (string.IsNullOrWhiteSpace(version)) return;

        try
        {
            var cleanVersion = version.Trim().TrimStart('V', 'v');
            var xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.xml");

            var doc = new XmlDocument();
            XmlElement? rootNode = null;

            if (File.Exists(xmlPath) && new FileInfo(xmlPath).Length > 0)
            {
                try
                {
                    doc.Load(xmlPath);
                    rootNode = doc.DocumentElement;
                }
                catch
                {
                    // 文件内容为空或损坏时容错，降级重新构建 XmlDocument
                    rootNode = null;
                }
            }

            if (rootNode == null)
            {
                doc = new XmlDocument();
                var xmlDec = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
                doc.AppendChild(xmlDec);
                rootNode = doc.CreateElement("item");
                doc.AppendChild(rootNode);
            }

            var currentXmlVerNode = rootNode.SelectSingleNode("version");
            var currentXmlVer = currentXmlVerNode?.InnerText?.Trim();

            // 若当前根节点已有版本号且与新版本号不同，则将原有根节点保存进 <history> 列表的头部
            if (!string.IsNullOrEmpty(currentXmlVer) && !string.Equals(currentXmlVer, cleanVersion, StringComparison.OrdinalIgnoreCase))
            {
                var historyNode = rootNode.SelectSingleNode("history") as XmlElement;
                if (historyNode == null)
                {
                    historyNode = doc.CreateElement("history");
                }

                var historyItem = doc.CreateElement("item");

                var oldVer = doc.CreateElement("version");
                oldVer.InnerText = currentXmlVer;
                historyItem.AppendChild(oldVer);

                var oldUrl = doc.CreateElement("url");
                oldUrl.InnerText = ToRelativeUrlPath(rootNode.SelectSingleNode("url")?.InnerText?.Trim());
                historyItem.AppendChild(oldUrl);

                var oldChangelog = doc.CreateElement("changelog");
                oldChangelog.InnerText = rootNode.SelectSingleNode("changelog")?.InnerText?.Trim() ?? string.Empty;
                historyItem.AppendChild(oldChangelog);

                var oldDate = doc.CreateElement("releaseDate");
                oldDate.InnerText = rootNode.SelectSingleNode("releaseDate")?.InnerText?.Trim() ?? string.Empty;
                historyItem.AppendChild(oldDate);

                // 插入到 history 节点顶部
                if (historyNode.HasChildNodes)
                {
                    historyNode.InsertBefore(historyItem, historyNode.FirstChild);
                }
                else
                {
                    historyNode.AppendChild(historyItem);
                }

                // 移除原有的 history 节点再统一重新追加置底
                if (historyNode.ParentNode == rootNode)
                {
                    rootNode.RemoveChild(historyNode);
                }
                rootNode.AppendChild(historyNode);
            }

            // 更新或设置当前根节点的属性字段（url 统一转为相对路径存储，不记录 IP 与端口号）
            SetOrUpdateXmlChild(doc, rootNode, "version", cleanVersion);
            if (!string.IsNullOrEmpty(url))
            {
                SetOrUpdateXmlChild(doc, rootNode, "url", ToRelativeUrlPath(url));
            }
            SetOrUpdateXmlChild(doc, rootNode, "changelog", changelog ?? string.Empty);
            SetOrUpdateXmlChild(doc, rootNode, "releaseDate", releaseTime ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            SetOrUpdateXmlChild(doc, rootNode, "mandatory", mandatory.ToString().ToLowerInvariant());

            // 保持 history 节点在 rootNode 子节点的最后
            var existingHistory = rootNode.SelectSingleNode("history");
            if (existingHistory != null)
            {
                rootNode.RemoveChild(existingHistory);
                rootNode.AppendChild(existingHistory);
            }

            // 统一清洗全部 url 节点（含根节点与 history 历史版本）：
            // 将存量数据中的绝对地址（含旧服务器 IP/端口）剥离主机部分，规范为相对路径存储
            var allUrlNodes = rootNode.SelectNodes("//url");
            if (allUrlNodes != null)
            {
                foreach (XmlNode urlNode in allUrlNodes)
                {
                    var rawUrl = urlNode.InnerText?.Trim();
                    if (!string.IsNullOrEmpty(rawUrl))
                    {
                        urlNode.InnerText = ToRelativeUrlPath(rawUrl);
                    }
                }
            }

            // 保存写回运行目录（统一带末尾换行 + 内容未变化时跳过写入，避免每次启动误标记文件已修改）
            SaveVersionXmlWithNewline(doc, xmlPath);

            // 同时双向同步更新写回源码工程目录（开发调试场景）
            TrySaveSourceProjectVersionXml(doc);

            await Task.CompletedTask;
        }
        catch
        {
            // 异常时静默处理
        }
    }

    /// <summary>
    /// 保存 version.xml：UTF-8（带 BOM）编码 + 文件末尾带换行符
    /// <para>问题 1：XmlDocument.Save 默认不输出末尾换行，git diff 会在文件尾显示一行 "-"（看似被追加了一个 -），
    /// 且每次启动文件被误标记为已修改。</para>
    /// <para>问题 2：StringWriter 默认 UTF-16，会把 XML 声明写成 encoding="utf-16" 而实际字节为 UTF-8，
    /// 下次启动 XmlDocument.Load 解析失败 → 触发"损坏重建"分支 → history 历史版本全部丢失。</para>
    /// <para>本方法统一以 UTF-8（带 BOM）写入并补齐末尾换行；内容与磁盘一致时跳过写入，避免时间戳抖动。</para>
    /// </summary>
    /// <param name="doc">待保存的 XmlDocument 实例</param>
    /// <param name="xmlPath">目标文件路径</param>
    private static void SaveVersionXmlWithNewline(XmlDocument doc, string xmlPath)
    {
        // 序列化到内存流（UTF-8 带 BOM，与原始 version.xml 文件编码保持一致）
        string content;
        var memoryStream = new MemoryStream();
        using (var xw = System.Xml.XmlWriter.Create(memoryStream, new System.Xml.XmlWriterSettings
        {
            Encoding = new System.Text.UTF8Encoding(true),
            Indent = true,
            IndentChars = "  ",
            OmitXmlDeclaration = false,
            NewLineChars = "\r\n",
            NewLineHandling = System.Xml.NewLineHandling.Replace
        }))
        {
            doc.Save(xw);
            xw.Flush();
        }
        content = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
        // 去除 GetString 对 BOM 解出的 BOM 字符（\uFEFF），由 WriteAllText 的编码参数负责写 BOM
        content = content.TrimStart('\uFEFF');
        // XML 声明编码名统一为大写 UTF-8（与仓库中原文件 encoding="UTF-8" 保持一致，避免 git 假修改）
        if (content.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>"))
            content = content.Replace("<?xml version=\"1.0\" encoding=\"utf-8\"?>", "<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        if (!content.EndsWith("\r\n"))
            content += "\r\n";

        // 内容与磁盘一致时跳过写入（避免每次启动都更新文件时间戳）
        try
        {
            if (File.Exists(xmlPath))
            {
                var existing = File.ReadAllText(xmlPath);
                if (string.Equals(existing, content, StringComparison.Ordinal))
                    return;
            }
        }
        catch
        {
            // 磁盘读取失败时继续执行写入
        }

        File.WriteAllText(xmlPath, content, new System.Text.UTF8Encoding(true));
    }

    /// <summary>
    /// 尝试双向同步写回源码工程项目目录下的 version.xml
    /// <para>方便开发调试时直接更新源码目录中的 version.xml，防止重新 Build 时被旧的空文件覆盖。</para>
    /// </summary>
    /// <param name="doc">需要保存的 XmlDocument 实例</param>
    private static void TrySaveSourceProjectVersionXml(XmlDocument doc)
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var dir = new DirectoryInfo(baseDir);

            // 向上逐级探测寻找包含 IMDP.Presentation.csproj 的源码工程目录
            while (dir != null)
            {
                var csprojPath = Path.Combine(dir.FullName, "IMDP.Presentation.csproj");
                if (File.Exists(csprojPath))
                {
                    var sourceXmlPath = Path.Combine(dir.FullName, "version.xml");
                    SaveVersionXmlWithNewline(doc, sourceXmlPath);
                    break;
                }
                dir = dir.Parent;
            }
        }
        catch
        {
            // 源码目录探测与写入失败时静默忽略
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 读取 AppVersionConfig 常量类中定义的最新版本号、更新说明、发布时间、下载地址与强更状态，
    /// 自动调用 UpdateVersionXmlAsync 将代码中的最新版本同步写回 version.xml（含原版本历史归档）。
    /// </remarks>
    public async Task SyncCodeVersionToXmlAsync()
    {
        await UpdateVersionXmlAsync(
            AppVersionConfig.Version,
            AppVersionConfig.ReleaseNotes,
            AppVersionConfig.DownloadUrl,
            AppVersionConfig.ReleaseTime,
            AppVersionConfig.Mandatory);
    }

    /// <summary>
    /// 设置或更新 XmlNode 的子节点文本
    /// </summary>
    private static void SetOrUpdateXmlChild(XmlDocument doc, XmlElement parent, string nodeName, string value)
    {
        var node = parent.SelectSingleNode(nodeName);
        if (node == null)
        {
            node = doc.CreateElement(nodeName);
            parent.AppendChild(node);
        }
        node.InnerText = value;
    }

    /// <summary>
    /// 解析 version.xml 清单，取版本号最大者
    /// <para>清单格式兼容 AutoUpdater.NET 标准 appcast（item 节点含 version/url/changelog/checksum），
    /// 同时支持多版本条目（item 节点集合），返回版本号最大的一条。</para>
    /// <para>url 节点存储相对路径（不含 IP 与端口），解析时自动与 UpdateXmlUrl 的服务器基准地址拼接为完整下载地址。</para>
    /// </summary>
    /// <param name="xml">version.xml 文本</param>
    /// <returns>版本号最大的版本信息；无有效条目返回 null</returns>
    private VersionInfo? ParseUpdateXml(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        var root = doc.DocumentElement;
        if (root == null) return null;

        var items = root.SelectNodes("//item");
        if (items == null || items.Count == 0) return null;

        VersionInfo? matched = null;
        Version? latestVer = null;
        foreach (XmlNode item in items)
        {
            var verStr = item.SelectSingleNode("version")?.InnerText?.Trim();
            var url = item.SelectSingleNode("url")?.InnerText?.Trim();
            var changelog = item.SelectSingleNode("changelog")?.InnerText?.Trim();
            var checksumNode = item.SelectSingleNode("checksum");
            var checksum = checksumNode?.Attributes?["value"]?.Value
                            ?? item.SelectSingleNode("sha256")?.InnerText?.Trim();

            if (string.IsNullOrEmpty(verStr) || string.IsNullOrEmpty(url))
                continue;

            var info = new VersionInfo
            {
                Version = verStr.StartsWith("V", StringComparison.OrdinalIgnoreCase) ? verStr : "V" + verStr,
                ReleaseDate = ParseReleaseDate(item),
                ReleaseNotes = changelog ?? string.Empty,
                DownloadUrl = ToAbsoluteUrl(url),
                CheckSum = checksum
            };

            // 选取版本号最大者
            var nv = NormalizeVersion(verStr);
            if (Version.TryParse(nv, out var parsed) && (latestVer == null || parsed > latestVer))
            {
                latestVer = parsed;
                matched = info;
            }
        }

        return matched;
    }

    /// <summary>
    /// 解析 version.xml 清单全部条目
    /// <para>使用 //item XPath 选取全部版本节点（包含根节点最新版与 &lt;history&gt; 容器内历史版本），
    /// 按版本号降序排列返回。AutoUpdater.NET 以 XmlSerializer 仅反序列化根 &lt;item&gt;，
    /// 忽略 &lt;history&gt; 容器，与 VersionView 的全量解析互不干扰。</para>
    /// <para>url 节点存储相对路径（不含 IP 与端口），解析时自动与 UpdateXmlUrl 的服务器基准地址拼接为完整下载地址。</para>
    /// </summary>
    /// <param name="xml">version.xml 文本</param>
    /// <returns>按版本号降序的全部版本条目；无有效条目返回空集合</returns>
    private IReadOnlyList<VersionInfo> ParseUpdateXmlAll(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        var root = doc.DocumentElement;
        if (root == null) return Array.Empty<VersionInfo>();

        var items = root.SelectNodes("//item");
        if (items == null || items.Count == 0) return Array.Empty<VersionInfo>();

        var list = new List<VersionInfo>();
        foreach (XmlNode item in items)
        {
            var verStr = item.SelectSingleNode("version")?.InnerText?.Trim();
            var url = item.SelectSingleNode("url")?.InnerText?.Trim();
            var changelog = item.SelectSingleNode("changelog")?.InnerText?.Trim();

            if (string.IsNullOrEmpty(verStr) || string.IsNullOrEmpty(url))
                continue;

            list.Add(new VersionInfo
            {
                Version = verStr.StartsWith("V", StringComparison.OrdinalIgnoreCase) ? verStr : "V" + verStr,
                ReleaseDate = ParseReleaseDate(item),
                ReleaseNotes = changelog ?? string.Empty,
                DownloadUrl = ToAbsoluteUrl(url)
            });
        }

        list.Sort((a, b) =>
        {
            var va = NormalizeVersion(a.Version);
            var vb = NormalizeVersion(b.Version);
            // TryParse 失败时回退 0.0.0.0，避免空引用
            var pv = Version.TryParse(vb, out var pbv) ? pbv : new Version(0, 0, 0, 0);
            var pa = Version.TryParse(va, out var pav) ? pav : new Version(0, 0, 0, 0);
            return pv.CompareTo(pa);
        });

        return list;
    }

    /// <summary>
    /// 解析版本条目的发布日期（releaseDate 节点）
    /// <para>节点缺失或日期格式无效时回退为当前时间（兼容无 releaseDate 节点的旧清单）。</para>
    /// </summary>
    /// <param name="item">版本条目 XML 节点</param>
    /// <returns>解析出的发布日期；解析失败返回当前时间</returns>
    private static DateTime ParseReleaseDate(XmlNode item)
    {
        var dateStr = item.SelectSingleNode("releaseDate")?.InnerText?.Trim();
        return DateTime.TryParse(dateStr, out var date) ? date : DateTime.Now;
    }

    /// <summary>
    /// 比较版本号，判断是否为新版本
    /// </summary>
    /// <param name="serverVersion">服务器版本号字符串</param>
    /// <param name="currentVersion">当前版本号字符串</param>
    /// <returns>服务器版本高于当前版本返回 true</returns>
    private static bool IsNewerVersion(string serverVersion, string currentVersion)
    {
        if (!Version.TryParse(NormalizeVersion(serverVersion), out var server))
            return false;
        if (!Version.TryParse(NormalizeVersion(currentVersion), out var current))
            return false;
        return server > current;
    }

    /// <summary>
    /// 规范化版本号字符串（去除 V 前缀，补齐为四段式以便 <see cref="Version"/> 解析）
    /// </summary>
    /// <param name="version">原始版本号字符串</param>
    /// <returns>可用于 <see cref="Version"/> 解析的字符串</returns>
    private static string NormalizeVersion(string version)
    {
        var v = (version ?? "1.0.0").Trim().TrimStart('V', 'v');
        var parts = v.Split('.');
        while (parts.Length < 4)
        {
            v += ".0";
            parts = v.Split('.');
        }
        return string.Join(".", parts.Take(4));
    }

    /// <summary>
    /// 从更新清单 URL 中提取服务器基准地址（协议://IP:端口）
    /// <para>示例：http://192.168.87.94:668/version.xml → http://192.168.87.94:668。</para>
    /// </summary>
    /// <returns>服务器基准地址字符串；UpdateXmlUrl 为空或非法时返回空字符串</returns>
    private string GetBaseUrl()
    {
        if (string.IsNullOrWhiteSpace(_updateXmlUrl))
            return string.Empty;
        try
        {
            var uri = new Uri(_updateXmlUrl);
            return uri.Scheme + "://" + uri.Authority;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 将相对路径拼接为完整下载地址
    /// <para>相对路径（如 /Package/xxx.zip）→ 基准地址 + 相对路径；
    /// 绝对地址 → 提取其路径部分与本地配置的基准地址重拼（强制与 appsettings.json 服务器对齐）。</para>
    /// <para>基准地址无效或 url 为空时原样返回，不抛异常。</para>
    /// </summary>
    /// <param name="url">version.xml 中存储的 url（相对路径或绝对地址）</param>
    /// <returns>拼接后的完整下载地址</returns>
    private string ToAbsoluteUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url ?? string.Empty;

        var baseUrl = GetBaseUrl();
        if (string.IsNullOrEmpty(baseUrl))
            return url;

        try
        {
            // 绝对地址：取路径部分（含查询串）与本地基准重拼，强制对齐本地配置的服务器
            if (Uri.TryCreate(url, UriKind.Absolute, out var absUri) && !string.IsNullOrEmpty(absUri.Authority))
                return baseUrl + absUri.PathAndQuery;

            // 相对路径：确保以 / 开头后拼接
            var path = url.Trim();
            if (!path.StartsWith("/"))
                path = "/" + path;
            return baseUrl + path;
        }
        catch
        {
            return url;
        }
    }

    /// <summary>
    /// 将下载地址转换为相对路径存储（剥离协议、IP 与端口，仅保留路径与查询串）
    /// <para>示例：http://192.168.87.95:667/Package/xxx.zip → /Package/xxx.zip；
    /// 传入已是相对路径时原样返回（仅补齐前导 /）。</para>
    /// </summary>
    /// <param name="url">原始下载地址</param>
    /// <returns>剥离主机部分后的相对路径</returns>
    private static string ToRelativeUrlPath(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        url = url.Trim();
        try
        {
            // 绝对地址：仅保留路径与查询串
            if (Uri.TryCreate(url, UriKind.Absolute, out var absUri) && !string.IsNullOrEmpty(absUri.Authority))
                return absUri.PathAndQuery;

            // 相对路径：确保以 / 开头
            return url.StartsWith("/") ? url : "/" + url;
        }
        catch
        {
            return url;
        }
    }

    /// <summary>
    /// 获取本机 IPv4 地址
    /// </summary>
    /// <returns>本机 IPv4 地址字符串；获取失败返回空字符串</returns>
    private static string GetLocalIpAddress()
    {
        try
        {
            var hostName = Dns.GetHostName();
            var addresses = Dns.GetHostAddresses(hostName);
            var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            return ipv4?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
