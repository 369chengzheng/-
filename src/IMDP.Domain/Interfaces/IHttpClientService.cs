// =============================================================================
// 文件名：IHttpClientService.cs
// 功能描述：统一 HTTP 客户端服务接口。
//           治理 HttpClient 分散创建问题（MesService 持静态实例、UpdateService 每次 new），
//           统一超时时间并复用单一 HttpClient，避免套接字耗尽。
// =============================================================================
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace IMDP.Domain.Interfaces;

/// <summary>
/// 统一 HTTP 客户端服务接口
/// <para>实现位于 Infrastructure 层（HttpClientService），由 DI 以单例注册。</para>
/// </summary>
public interface IHttpClientService
{
    /// <summary>
    /// GET 请求并返回响应正文字符串。
    /// </summary>
    /// <param name="url">请求地址。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>响应正文。</returns>
    Task<string> GetStringAsync(string url, CancellationToken ct = default);

    /// <summary>
    /// 以 application/json 发送 POST 请求，返回原始响应对象（保留状态码等判定能力）。
    /// </summary>
    /// <param name="url">请求地址。</param>
    /// <param name="json">JSON 正文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>原始 HTTP 响应，调用方负责释放。</returns>
    Task<HttpResponseMessage> PostJsonAsync(string url, string json, CancellationToken ct = default);
}
