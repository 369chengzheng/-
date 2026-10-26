// =============================================================================
// 文件名：MesService.cs
// 功能描述：MES 系统服务实现，通过 HTTP 与制造执行系统通信。
//            CheckUserInfo / GetProductInfo / GetProductInfo2 / SaveProcedure /
//            SaveProcedure2 / GetTestResult / GetTestResult_LastNotNull /
//            GetTestResult_1N / SaveTestRecord 走 HTTP
//            POST 到 /mc/http/interface.ms?model=FactorySettings&method=xxx）；
//            制令单查询 GetProductInfoByMo 走独立 HTTP。
// =============================================================================
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using IMDP.Domain.Interfaces;
using Newtonsoft.Json;
using NLog;

using IMDP.Infrastructure.Logging;
namespace IMDP.Infrastructure.Services;

/// <summary>
/// MES 系统服务实现
/// </summary>
public class MesService : IMesService
{
    private static readonly ILogger _logger = LogHelper.For<MesService>();

    // 兜底默认值（MES FactorySettings 接口基址，由 appsettings.json Mes:Mo:Url 注入）
    private const string DefaultMoUrl = "http://192.168.18.88:8080/mc/http/interface.ms?model=FactorySettings&method=GetProductInfoByMo";

    // MES FactorySettings 接口基址（由 appsettings.json Mes:Mo:Url 注入）
    private readonly string _moUrl;

    // HTTP 客户端（静态共享，避免每次请求重建套接字；与现有简洁风格一致）
    private readonly IHttpClientService _httpClient;

    /// <summary>
    /// 无参构造（兜底，使用源码默认 URL）。
    /// <para>正式部署应通过 DI 工厂注入 appsettings.json 的 Mes:Mo 配置；
    /// 使用此构造会记录一条警告。</para>
    /// </summary>
    public MesService() : this(DefaultMoUrl)
    {
        _logger.Warn("MesService 使用无参构造，Mo 接口将采用源码默认 URL。正式部署应通过 DI 工厂注入 appsettings.json 的 Mes:Mo 配置。");
    }

    /// <summary>
    /// 构造函数（由 DI 工厂注入 appsettings.json 的 Mes:Mo 配置）。
    /// </summary>
    /// <param name="moUrl">MES FactorySettings 接口基址（对应 Mes:Mo:Url，承载 GetProductInfoByMo 及全部 EqOperation 接口）</param>
    public MesService(string moUrl, IHttpClientService? httpClientService = null)
    {
        _moUrl = string.IsNullOrWhiteSpace(moUrl) ? DefaultMoUrl : moUrl;
        _httpClient = httpClientService ?? new HttpClientService();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 委托 <see cref="GetProductInfoByMoAsync"/> 查询完整产品信息，取软件清单首个
    /// <see cref="MesMoSoftwareItem.SoftItemCode"/> 返回。查询失败或清单为空时返回 null。
    /// </remarks>
    public async Task<string?> GetMaterialNumberAsync(string orderNumber)
    {
        var result = await GetProductInfoByMoAsync(orderNumber).ConfigureAwait(false);
        if (!result.Success || result.ProductInfo?.SoftwareList is null || result.ProductInfo.SoftwareList.Count == 0)
            return null;
        return result.ProductInfo.SoftwareList[0].SoftItemCode;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 对应「制令单软件物料号信息查询」HTTP 接口：
    /// POST <c>{"moNumber": "..."}</c> 到 Mes:Mo:Url，返回 RuResult/ErrorInfo/ProductInfo。
    /// RuResult==1 表示查询成功。响应字段大小写混合，反序列化 DTO 用 [JsonProperty] 显式映射。
    /// </remarks>
    public async Task<MesProductInfoByMoResult> GetProductInfoByMoAsync(string moNumber)
    {
        if (string.IsNullOrWhiteSpace(moNumber))
            return new MesProductInfoByMoResult { Success = false, ErrorInfo = "moNumber 不能为空。" };

        try
        {
            var body = new { moNumber };
            var json = JsonConvert.SerializeObject(body);
#if DEBUG
            // DEBUG 专用：将上传 MES 的数据序列化为缩进 JSON 打印到日志，便于人工验证（参考 IMDS jsonContent 日志）
            _logger.Info($"[MES][GetProductInfoByMo][DEBUG] 上传JSON：\n{JsonConvert.SerializeObject(body, Formatting.Indented)}");
#endif
            using var resp = await _httpClient.PostJsonAsync(_moUrl, json).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                var msg = $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}";
                _logger.Warn($"GetProductInfoByMo 请求失败：{msg}（moNumber={moNumber}）");
                return new MesProductInfoByMoResult { Success = false, ErrorInfo = "调用制令单查询接口失败：" + msg };
            }

            var text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var dto = JsonConvert.DeserializeObject<MoResponseDto>(text);
            if (dto == null)
                return new MesProductInfoByMoResult { Success = false, ErrorInfo = "制令单查询接口返回空响应。" };

            if (dto.RuResult != 1)
                return new MesProductInfoByMoResult { Success = false, ErrorInfo = dto.ErrorInfo };

            var p = dto.ProductInfo;
            var info = new MesMoProductInfo
            {
                PlanQty = p?.PlanQty ?? 0,
                ProductCode = p?.ProductCode,
                MoNumber = p?.MoNumber,
                CustName = p?.CustName,
                CustCode = p?.CustCode,
                ProductName = p?.ProductName,
                ProductSpec = p?.ProductSpec,
                SoftwareList = (p?.SoftwareList ?? Array.Empty<MoSoftwareDto>())
                    .Select(s => new MesMoSoftwareItem { SoftItemCode = s.SoftItemCode, MainBoardCode = s.MainBoardCode })
                    .ToList()
            };
            return new MesProductInfoByMoResult { Success = true, ProductInfo = info };
        }
        catch (HttpRequestException ex)
        {
            _logger.Warn(ex, $"GetProductInfoByMo 网络异常（moNumber={moNumber}）：{ex.Message}");
            return new MesProductInfoByMoResult { Success = false, ErrorInfo = "制令单查询接口网络异常：" + ex.Message };
        }
        catch (JsonException ex)
        {
            _logger.Warn(ex, $"GetProductInfoByMo 响应解析失败（moNumber={moNumber}）：{ex.Message}");
            return new MesProductInfoByMoResult { Success = false, ErrorInfo = "制令单查询接口响应解析失败：" + ex.Message };
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, $"GetProductInfoByMo 异常（moNumber={moNumber}）：{ex.Message}");
            return new MesProductInfoByMoResult { Success = false, ErrorInfo = "制令单查询接口异常：" + ex.Message };
        }
    }

    // ===== 9 个 EqOperation 接口的 HTTP 实现 =====
    // POST JSON 到 /mc/http/interface.ms?model=FactorySettings&method=xxx，
    // 响应统一含 RuResult（1=成功）+ ErrorInfo。其余 6 个按 EqOperation 签名推断（注释标注 🟡），字段名不符时仅业务字段为 null，
    // 修正只需调整下方私有响应 DTO 的 [JsonProperty] 名，方法签名不变。

    /// <inheritdoc/>
    /// <remarks>
    /// 对应 AutoFrock MESMethod::CheckUserInfo。URL method=CheckUser。
    /// 请求 JSON：<c>{"UserCode":..., "PWord":..., "Rescode":...}</c>。
    /// 响应：<c>{RuResult, ErrorInfo}</c>，RuResult==1 表示校验通过。
    /// </remarks>
    public async Task<MesCheckUserResult> CheckUserInfoAsync(string userCode, string password, string resCode)
    {
        var body = new { UserCode = userCode ?? string.Empty, PWord = password ?? string.Empty, Rescode = resCode ?? string.Empty };
        var (resp, err) = await PostMesAsync("CheckUser", body).ConfigureAwait(false);
        if (resp == null)
            return new MesCheckUserResult { Success = false, ErrorInfo = err };

        return new MesCheckUserResult { Success = resp.RuResult == 1, ErrorInfo = resp.ErrorInfo };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 对应 AutoFrock MESMethod::GetProductInfo。URL method=GetProductInfo。
    /// 请求 JSON：<c>{"ProBar":..., "Rescode":...}</c>。
    /// 响应：RuResult/ErrorInfo 之外，ProductInfo / TestClassinfo 字段名按 EqOperation out 参数推断 🟡，需对照 MES 后端确认。
    /// </remarks>
    public async Task<MesProductInfoResult> GetProductInfoAsync(string proBar, string resCode)
    {
        var body = new { ProBar = proBar ?? string.Empty, Rescode = resCode ?? string.Empty };
        var (resp, err) = await PostMesAsync("GetProductInfo", body).ConfigureAwait(false);
        if (resp == null)
            return new MesProductInfoResult { Success = false, ErrorInfo = err };

        return new MesProductInfoResult
        {
            Success = resp.RuResult == 1,
            ErrorInfo = resp.ErrorInfo,
            ProductInfo = resp.ProductInfo,
            TestClassinfo = resp.TestClassinfo
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 对应 AutoFrock MESMethod::GetProductInfo2。URL method=GetProductInfo2。
    /// 请求 JSON：<c>{"AssyMoNumber":..., "ProBar":..., "Rescode":...}</c>。
    /// 响应 ProductInfo / TestClassinfo 字段名同上 🟡。
    /// </remarks>
    public async Task<MesProductInfoResult> GetProductInfo2Async(string assyMoNumber, string proBar, string resCode)
    {
        var body = new { AssyMoNumber = assyMoNumber ?? string.Empty, ProBar = proBar ?? string.Empty, Rescode = resCode ?? string.Empty };
        var (resp, err) = await PostMesAsync("GetProductInfo2", body).ConfigureAwait(false);
        if (resp == null)
            return new MesProductInfoResult { Success = false, ErrorInfo = err };

        return new MesProductInfoResult
        {
            Success = resp.RuResult == 1,
            ErrorInfo = resp.ErrorInfo,
            ProductInfo = resp.ProductInfo,
            TestClassinfo = resp.TestClassinfo
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 对应 AutoFrock MESMethod::SaveProcedure。URL method=SaveProcedure。
    /// 请求 JSON：<c>{"ProBar":..., "IResult":..., "UserCode":..., "Rescode":..., "TestResult":..., "Errcode":...}</c>
    /// （IMDS 中 testResult 为嵌套对象；本处 testResult 入参已是组装好的字符串，按 EqOperation 简单字段推断 🟡，需对照 MES 后端确认）。
    /// 响应：<c>{RuResult, ErrorInfo}</c>。
    /// </remarks>
    public async Task<MesOperationResult> SaveProcedureAsync(string proBar, int result, string userCode, string resCode, string testResult, string errCode)
    {
        var body = new
        {
            ProBar = proBar ?? string.Empty,
            IResult = result,
            UserCode = userCode ?? string.Empty,
            Rescode = resCode ?? string.Empty,
            TestResult = testResult ?? string.Empty,
            Errcode = errCode ?? string.Empty
        };
        var (resp, err) = await PostMesAsync("SaveProcedure", body).ConfigureAwait(false);
        if (resp == null)
            return new MesOperationResult { Success = false, ErrorInfo = err };

        return new MesOperationResult { Success = resp.RuResult == 1, ErrorInfo = resp.ErrorInfo };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 对应 AutoFrock MESMethod::SaveProcedure2。URL method=SaveProcedure2（按 EqOperation 签名推断 🟡）。
    /// 请求 JSON：在 SaveProcedure 基础上增加 <c>AssyMoNumber</c>（推断 🟡）。
    /// </remarks>
    public async Task<MesOperationResult> SaveProcedure2Async(string assyMoNumber, string proBar, int result, string userCode, string resCode, string testResult, string errCode)
    {
        var body = new
        {
            AssyMoNumber = assyMoNumber ?? string.Empty,
            ProBar = proBar ?? string.Empty,
            IResult = result,
            UserCode = userCode ?? string.Empty,
            Rescode = resCode ?? string.Empty,
            TestResult = testResult ?? string.Empty,
            Errcode = errCode ?? string.Empty
        };
        var (resp, err) = await PostMesAsync("SaveProcedure2", body).ConfigureAwait(false);
        if (resp == null)
            return new MesOperationResult { Success = false, ErrorInfo = err };

        return new MesOperationResult { Success = resp.RuResult == 1, ErrorInfo = resp.ErrorInfo };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// IMDS 兼容载荷版（对应 IMDS MesService.SaveProcedure）：
    /// ① GetProductInfo 工序卡控（RuResult != 1 直接失败，不重复上传）；
    /// ② 解析 MES 下发的 TestClassinfo（Test_info.test_standard_array.test_item[]），
    ///    按 ti_id 匹配本地测试数据并回填 ti_Class（未配置项记 Warn 日志跳过）；
    /// ③ 任一匹配项 Result != "1" 时整体 IResult 置 0，Errcode 拼接 ",BL-项目码"（去 C-/Z- 前缀）；
    /// ④ 按 IMDS 嵌套结构（TestResult.test_result.product_result_item.product_result[]）POST SaveProcedure。
    /// </remarks>
    public async Task<MesOperationResult> SaveProcedureAsync(string proBar, int result, string userCode, string resCode, IReadOnlyList<MesProductItem> testItems)
    {
        ArgumentNullException.ThrowIfNull(testItems);

        // ① 工序卡控：不符合工序直接失败（对应 IMDS SaveProcedure 开头的 GetProductInfo 检查）
        var productInfo = await GetProductInfoAsync(proBar, resCode).ConfigureAwait(false);
        if (!productInfo.Success || string.IsNullOrEmpty(productInfo.TestClassinfo))
        {
            string gateError = productInfo.Success ? "MES 未下发测试类信息（TestClassinfo 为空）" : productInfo.ErrorInfo;
            _logger.Warn($"SaveProcedure 工序卡控未通过：proBar={proBar}，resCode={resCode}，原因={gateError}");
            return new MesOperationResult { Success = false, ErrorInfo = "工序卡控未通过：" + gateError };
        }

        // ② 解析 MES 测试项列表，匹配本地数据回填 ti_Class
        List<MesTestItemDto>? mesTestItems = TryParseMesTestItems(productInfo.TestClassinfo);
        if (mesTestItems == null || mesTestItems.Count == 0)
        {
            _logger.Warn($"SaveProcedure TestClassinfo 解析失败或测试项为空：proBar={proBar}，TestClassinfo={productInfo.TestClassinfo}");
            return new MesOperationResult { Success = false, ErrorInfo = "MES 测试类信息解析失败" };
        }

        var upItems = new List<SaveProcedureProductResult>();
        var unconfiguredCodes = new StringBuilder();
        int overall = testItems.Count > 0 ? 1 : 0;
        string ngCode = string.Empty;

        foreach (var item in testItems)
        {
            var matched = mesTestItems.FirstOrDefault(m => m.Code == item.Code);
            if (matched == null)
            {
                // 未在 MES 配置的检测项目：跳过上传（对应 IMDS unConfigCode 日志）
                unconfiguredCodes.Append(item.Code).Append(' ');
                continue;
            }

            upItems.Add(new SaveProcedureProductResult
            {
                Code = item.Code,
                Value = item.Data,
                Result = item.Result,
                Class = matched.Class
            });
            if (item.Result != "1")
            {
                overall = 0;
                ngCode += ",BL-" + item.Code.Replace("C-", string.Empty).Replace("Z-", string.Empty);
            }
        }

        if (unconfiguredCodes.Length > 0)
        {
            _logger.Info($"SaveProcedure 未配置的检测项目：{unconfiguredCodes}");
        }

        if (result == 1 && overall == 0)
        {
            // 调用方判合格但存在单项未通过：以单项综合为准（对应 IMDS IsPass 由单项推导）
            _logger.Warn($"SaveProcedure 调用方判定合格但存在单项未通过，按单项综合置为不合格：proBar={proBar}");
        }

        // ③ 组 IMDS 嵌套载荷并 POST
        var payload = new SaveProcedurePayload
        {
            ProBar = proBar ?? string.Empty,
            IResult = overall,
            UserCode = userCode ?? string.Empty,
            Rescode = resCode ?? string.Empty,
            Errcode = ngCode,
            TestResult = new SaveProcedureTestResult
            {
                TestResultInner = new SaveProcedureTestResultInner
                {
                    Channel = "1",
                    Result = overall.ToString(),
                    ProductResultItem = new SaveProcedureProductResultItem
                    {
                        Dim = upItems.Count.ToString(),
                        ProductResultList = upItems
                    }
                }
            }
        };

        string json;
        try { json = JsonConvert.SerializeObject(payload); }
        catch (Exception ex)
        {
            _logger.Warn(ex, $"SaveProcedure 载荷序列化失败：{ex.Message}");
            return new MesOperationResult { Success = false, ErrorInfo = "载荷序列化失败：" + ex.Message };
        }

        _logger.Info($"SaveProcedure 上传载荷：proBar={proBar}，匹配项={upItems.Count}，整体判定={overall}，JSON={json}");

        var (resp, err) = await PostMesAsync("SaveProcedure", payload).ConfigureAwait(false);
        if (resp == null)
            return new MesOperationResult { Success = false, ErrorInfo = err };

        if (resp.RuResult != 1)
        {
            _logger.Warn($"SaveProcedure MES 拒收：proBar={proBar}，RuResult={resp.RuResult}，ErrorInfo={resp.ErrorInfo}");
            return new MesOperationResult { Success = false, ErrorInfo = resp.ErrorInfo };
        }

        return new MesOperationResult { Success = true };
    }

    /// <summary>
    /// 解析 MES 下发的测试类信息 JSON（对应 IMDS TestClassInfo 嵌套结构：
    /// Test_info.test_standard_array.test_item[]，字段 ti_id/ti_Class）
    /// </summary>
    /// <param name="testClassinfo">GetProductInfo 返回的 TestClassinfo JSON 串</param>
    /// <returns>MES 测试项列表；解析失败时返回 null</returns>
    private static List<MesTestItemDto>? TryParseMesTestItems(string testClassinfo)
    {
        try
        {
            var dto = JsonConvert.DeserializeObject<MesTestClassInfoDto>(testClassinfo);
            return dto?.TestInfo?.TestStandardArray?.TestItems;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // ===== SaveProcedure（IMDS 兼容版）载荷 DTO（对应 IMDS Base.Models.SaveProcedure / Product） =====

    /// <summary>MES 测试类信息响应嵌套（对应 IMDS TestClassInfo）</summary>
    private class MesTestClassInfoDto
    {
        [JsonProperty("Test_info")] public MesTestInfoDto? TestInfo { get; set; }
    }

    /// <summary>测试信息（对应 IMDS TestInfo）</summary>
    private class MesTestInfoDto
    {
        [JsonProperty("test_standard_array")] public MesTestStandardArrayDto? TestStandardArray { get; set; }
    }

    /// <summary>测试标准数组（对应 IMDS TestStandardArray）</summary>
    private class MesTestStandardArrayDto
    {
        [JsonProperty("dim")] public string? Dim { get; set; }

        [JsonProperty("test_item")] public List<MesTestItemDto>? TestItems { get; set; }
    }

    /// <summary>MES 测试项（对应 IMDS MesTestItem，取 ti_id 与 ti_Class）</summary>
    private class MesTestItemDto
    {
        [JsonProperty("ti_id")] public string Code { get; set; } = string.Empty;

        [JsonProperty("ti_Class")] public string Class { get; set; } = string.Empty;
    }

    /// <summary>SaveProcedure 上传载荷顶层（对应 IMDS SaveProcedure）</summary>
    private class SaveProcedurePayload
    {
        [JsonProperty("ProBar")] public string ProBar { get; set; } = string.Empty;

        [JsonProperty("IResult")] public int IResult { get; set; }

        [JsonProperty("UserCode")] public string UserCode { get; set; } = string.Empty;

        [JsonProperty("Rescode")] public string Rescode { get; set; } = string.Empty;

        [JsonProperty("Errcode")] public string Errcode { get; set; } = string.Empty;

        [JsonProperty("TestResult")] public SaveProcedureTestResult? TestResult { get; set; }
    }

    /// <summary>测试结果包装（序列化为 test_result）</summary>
    private class SaveProcedureTestResult
    {
        [JsonProperty("test_result")] public SaveProcedureTestResultInner? TestResultInner { get; set; }
    }

    /// <summary>测试结果体（通道/整体结果/结果项）</summary>
    private class SaveProcedureTestResultInner
    {
        [JsonProperty("channel")] public string Channel { get; set; } = "1";

        [JsonProperty("result")] public string Result { get; set; } = string.Empty;

        [JsonProperty("product_result_item")] public SaveProcedureProductResultItem? ProductResultItem { get; set; }
    }

    /// <summary>结果项集合（@dim = 项数）</summary>
    private class SaveProcedureProductResultItem
    {
        [JsonProperty("@dim")] public string Dim { get; set; } = string.Empty;

        [JsonProperty("product_result")] public List<SaveProcedureProductResult>? ProductResultList { get; set; }
    }

    /// <summary>单项结果（ti_id/pri_data/pri_result/ti_Class）</summary>
    private class SaveProcedureProductResult
    {
        [JsonProperty("ti_id")] public string Code { get; set; } = string.Empty;

        [JsonProperty("pri_data")] public string Value { get; set; } = string.Empty;

        [JsonProperty("pri_result")] public string Result { get; set; } = string.Empty;

        [JsonProperty("ti_Class")] public string Class { get; set; } = string.Empty;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 对应 AutoFrock MESMethod::GetProductTestData（EqOperation::GetTestResult）。URL method=GetTestResult（推断 🟡）。
    /// 请求 JSON：<c>{"MeterCode":..., "Num":n, "Ids":[...]}</c>（推断 🟡）。
    /// 响应 Data[] / Name[] 字段名推断 🟡；取 <c>Data[0..num-1]</c> 作为 TestData，对应 AutoFrock cs_Data 读取逻辑。
    /// </remarks>
    public async Task<MesTestDataResult> GetProductTestDataAsync(string proBar, IReadOnlyList<string> testCodes)
    {
        int num = testCodes.Count;
        if (num <= 0 || num > 256)
            return new MesTestDataResult { Success = false, ErrorInfo = "testCodes 数量须在 1~256 之间（对应 AutoFrock Num 限制）。", TestData = Array.Empty<string>() };

        var body = new
        {
            MeterCode = proBar ?? string.Empty,
            Num = num,
            Ids = Enumerable.Range(0, num).Select(i => testCodes[i] ?? string.Empty).ToArray()
        };
        var (resp, err) = await PostMesAsync("GetTestResult", body).ConfigureAwait(false);
        if (resp == null)
            return new MesTestDataResult { Success = false, ErrorInfo = err, TestData = Array.Empty<string>() };

        var result = new List<string>(num);
        if (resp.Data != null)
        {
            for (int i = 0; i < num && i < resp.Data.Length; i++)
                result.Add(resp.Data[i] ?? string.Empty);
        }
        return new MesTestDataResult
        {
            Success = resp.RuResult == 1,
            ErrorInfo = resp.ErrorInfo,
            TestData = result
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 对应 AutoFrock MESMethod::GetProductTestData_LastNotNull（EqOperation::GetTestResult_LastNotNull）。URL method=GetTestResult_LastNotNull（推断 🟡）。
    /// 请求 JSON：<c>{"MeterCode":..., "Id":...}</c>（推断 🟡）。
    /// 响应 Data 字段名推断 🟡；返回单个 TestData。
    /// </remarks>
    public async Task<MesTestDataResult> GetProductTestDataLastNotNullAsync(string proBar, string testCode)
    {
        if (string.IsNullOrEmpty(proBar) || string.IsNullOrEmpty(testCode))
            return new MesTestDataResult { Success = false, ErrorInfo = "proBar / testCode 不能为空。", TestData = Array.Empty<string>() };

        var body = new { MeterCode = proBar, Id = testCode };
        var (resp, err) = await PostMesAsync("GetTestResult_LastNotNull", body).ConfigureAwait(false);
        if (resp == null)
            return new MesTestDataResult { Success = false, ErrorInfo = err, TestData = Array.Empty<string>() };

        return new MesTestDataResult
        {
            Success = resp.RuResult == 1,
            ErrorInfo = resp.ErrorInfo,
            TestData = new[] { resp.Data is { Length: > 0 } ? resp.Data[0] ?? string.Empty : string.Empty }
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 对应 AutoFrock MESMethod::GetTestResult_1N。URL method=GetTestResult_1N。
    /// 请求 JSON：<c>{"MeterCode":..., "ItemCode":...}</c>。
    /// 响应 Data[] / Class[] 字段名推断 🟡；遍历 Class/Data，同 key（项目类型）仅保留首个
    /// （对应 AutoFrock <c>_mapItemData.Lookup</c> 取首逻辑）。
    /// </remarks>
    public async Task<MesTestResult1NResult> GetTestResult1NAsync(string meterCode, string itemCode)
    {
        if (string.IsNullOrEmpty(meterCode) || string.IsNullOrEmpty(itemCode))
            return new MesTestResult1NResult { Success = false, ErrorInfo = "meterCode / itemCode 不能为空。" };

        var body = new { MeterCode = meterCode, ItemCode = itemCode };
        var (resp, err) = await PostMesAsync("GetTestResult_1N", body).ConfigureAwait(false);
        if (resp == null)
            return new MesTestResult1NResult { Success = false, ErrorInfo = err };

        var items = new Dictionary<string, string>();
        var data = resp.Data;
        var classArr = resp.Class;
        if (data != null && classArr != null)
        {
            int len = Math.Min(data.Length, classArr.Length);
            for (int i = 0; i < len; i++)
            {
                string? k = classArr[i];
                string? v = data[i];
                // 对应 AutoFrock：cs_Class[i]/cs_Data[i] 非 null 时加入；同 key 仅取首个
                if (!string.IsNullOrEmpty(k) && v != null && !items.ContainsKey(k))
                    items[k] = v;
            }
        }
        return new MesTestResult1NResult
        {
            Success = resp.RuResult == 1,
            ErrorInfo = resp.ErrorInfo,
            Items = items
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 对应 AutoFrock MESMethod::SaveTestRecord（EqOperation::SaveTestRecord 26 参数：proBar+result+userCode+resCode+Record1..Record20+iRuResult+errorInfo）。
    /// URL method=SaveTestRecord（推断 🟡）。请求 JSON 字段 Record1..Record20 推断 🟡；
    /// records 不足 20 时尾部补空串（对应 AutoFrock <c>SplitString</c> 后 <c>for(i=iCol;i&lt;20;i++) _arrayRecord.Add("")</c>）。
    /// 响应：<c>{RuResult, ErrorInfo}</c>。
    /// </remarks>
    public async Task<MesOperationResult> SaveTestRecordAsync(string proBar, int result, string userCode, string resCode, IReadOnlyList<string> records)
    {
        // 固定 20 槽位，不足尾部补空串
        var recs = new string[20];
        for (int i = 0; i < 20; i++)
            recs[i] = i < records.Count ? (records[i] ?? string.Empty) : string.Empty;

        var body = new
        {
            ProBar = proBar ?? string.Empty,
            IResult = result,
            UserCode = userCode ?? string.Empty,
            Rescode = resCode ?? string.Empty,
            Record1 = recs[0], Record2 = recs[1], Record3 = recs[2], Record4 = recs[3], Record5 = recs[4],
            Record6 = recs[5], Record7 = recs[6], Record8 = recs[7], Record9 = recs[8], Record10 = recs[9],
            Record11 = recs[10], Record12 = recs[11], Record13 = recs[12], Record14 = recs[13], Record15 = recs[14],
            Record16 = recs[15], Record17 = recs[16], Record18 = recs[17], Record19 = recs[18], Record20 = recs[19]
        };
        var (resp, err) = await PostMesAsync("SaveTestRecord", body).ConfigureAwait(false);
        if (resp == null)
            return new MesOperationResult { Success = false, ErrorInfo = err };

        return new MesOperationResult { Success = resp.RuResult == 1, ErrorInfo = resp.ErrorInfo };
    }

    // ===== HTTP 辅助（仿 IMDS Base/Service/MesService.GetUrl + GetPostInfo） ====
    private string GetMesUrl(string method)
    {
        var url = string.IsNullOrWhiteSpace(_moUrl) ? DefaultMoUrl : _moUrl;
        var idx = url.IndexOf("method=", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            return url.Substring(0, idx + "method=".Length) + method;
        // 兜底：原 URL 不含 method 参数时直接拼接
        return url + (url.Contains('?') ? "&" : "?") + "method=" + method;
    }

    /// <summary>
    /// POST JSON 到 MES FactorySettings 接口，返回响应 DTO 与错误信息。
    /// <para>失败/异常时返回 (null, 错误信息)；成功时反序列化为 <see cref="MesBaseResp"/>（业务字段缺失为 null，多余字段忽略）。</para>
    /// </summary>
    private async Task<(MesBaseResp? resp, string? error)> PostMesAsync<T>(string method, T body)
    {
        string json;
        try { json = JsonConvert.SerializeObject(body); }
        catch (Exception ex)
        {
            _logger.Warn(ex, $"MES {method} 请求序列化失败：{ex.Message}");
            return (null, "MES " + method + " 请求序列化失败：" + ex.Message);
        }

#if DEBUG
        // DEBUG 专用：将上传 MES 的数据序列化为缩进 JSON 打印到日志，便于人工验证（参考 IMDS jsonContent 日志）
        _logger.Info($"[MES][{method}][DEBUG] 上传JSON：\n{JsonConvert.SerializeObject(body, Formatting.Indented)}");
#endif

        try
        {
            var url = GetMesUrl(method);
            using var resp = await _httpClient.PostJsonAsync(url, json).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                var msg = $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}";
                _logger.Warn($"MES {method} 请求失败：{msg}");
                return (null, "调用 MES " + method + " 失败：" + msg);
            }

            var text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#if DEBUG
            // DEBUG 专用：打印 MES 原始响应，便于人工比对（参考 IMDS resultContent 日志）
            _logger.Info($"[MES][{method}][DEBUG] 响应JSON：\n{text}");
#endif
            var dto = JsonConvert.DeserializeObject<MesBaseResp>(text);
            if (dto == null)
                return (null, "MES " + method + " 返回空响应。");
            return (dto, null);
        }
        catch (HttpRequestException ex)
        {
            _logger.Warn(ex, $"MES {method} 网络异常：{ex.Message}");
            return (null, "MES " + method + " 网络异常：" + ex.Message);
        }
        catch (JsonException ex)
        {
            _logger.Warn(ex, $"MES {method} 响应解析失败：{ex.Message}");
            return (null, "MES " + method + " 响应解析失败：" + ex.Message);
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, $"MES {method} 异常：{ex.Message}");
            return (null, "MES " + method + " 异常：" + ex.Message);
        }
    }

    // ===== MES FactorySettings 接口响应 DTO =====
    // 基础响应：所有 method 均含 RuResult/ErrorInfo；业务字段在子 DTO 中按推断命名，
    // Newtonsoft 默认缺字段为 null、多余字段忽略，故字段名不符不会抛异常，仅业务字段为 null。

    private class MesBaseResp
    {
        [JsonProperty("RuResult")] public int RuResult { get; set; }
        [JsonProperty("ErrorInfo")] public string? ErrorInfo { get; set; }
        // GetProductInfo / GetProductInfo2 业务字段（按 EqOperation out 参数推断 🟡）
        [JsonProperty("ProductInfo")] public string? ProductInfo { get; set; }
        [JsonProperty("TestClassinfo")] public string? TestClassinfo { get; set; }
        // GetTestResult 业务字段（推断 🟡）
        [JsonProperty("Data")] public string?[]? Data { get; set; }
        [JsonProperty("Name")] public string?[]? Name { get; set; }
        // GetTestResult_1N 业务字段：key=项目类型 Class，value=数据 Data（推断 🟡）
        [JsonProperty("Class")] public string?[]? Class { get; set; }
    }

    // ===== 制令单查询接口反序列化 DTO =====
    // 响应字段大小写混合（顶层 PascalCase，嵌套 camelCase），用 [JsonProperty] 显式映射。
    private class MoResponseDto
    {
        [JsonProperty("RuResult")] public int RuResult { get; set; }
        [JsonProperty("ErrorInfo")] public string? ErrorInfo { get; set; }
        [JsonProperty("ProductInfo")] public MoProductInfoDto? ProductInfo { get; set; }
    }

    private class MoProductInfoDto
    {
        [JsonProperty("planQty")] public int PlanQty { get; set; }
        [JsonProperty("productCode")] public string? ProductCode { get; set; }
        [JsonProperty("moNumber")] public string? MoNumber { get; set; }
        [JsonProperty("custName")] public string? CustName { get; set; }
        [JsonProperty("custCode")] public string? CustCode { get; set; }
        [JsonProperty("productName")] public string? ProductName { get; set; }
        [JsonProperty("productSpec")] public string? ProductSpec { get; set; }
        [JsonProperty("softwareList")] public MoSoftwareDto[]? SoftwareList { get; set; }
    }

    private class MoSoftwareDto
    {
        [JsonProperty("softItemCode")] public string? SoftItemCode { get; set; }
        [JsonProperty("mainBoardCode")] public string? MainBoardCode { get; set; }
    }
}
