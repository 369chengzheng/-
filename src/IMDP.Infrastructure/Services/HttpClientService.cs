// =============================================================================
// 文件名：HttpClientService.cs
// 功能描述：IHttpClientService 的实现。内部持有唯一静态 HttpClient 实例，
//           统一 15 秒超时，避免各处重复创建 HttpClient 导致的套接字耗尽。
// =============================================================================
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IMDP.Domain.Interfaces;

namespace IMDP.Infrastructure.Services;

/// <summary>
/// 统一 HTTP 客户端服务实现
/// <para>底层 HttpClient 为静态单例并被复用，请勿对方法返回的 HttpResponseMessage
/// 做短生命周期假设之外的操作（由调用方 using 释放即可）。</para>
/// </summary>
public class HttpClientService : IHttpClientService
{
    /// <summary>复用的 HttpClient 实例（统一 15 秒超时）。</summary>
    private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

    /// <inheritdoc />
    public async Task<string> GetStringAsync(string url, CancellationToken ct = default)
        => await _http.GetStringAsync(url, ct).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<HttpResponseMessage> PostJsonAsync(string url, string json, CancellationToken ct = default)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _http.PostAsync(url, content, ct).ConfigureAwait(false);
    }
}
