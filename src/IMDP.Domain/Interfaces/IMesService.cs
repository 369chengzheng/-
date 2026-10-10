// =============================================================================
// 文件名：IMesService.cs
// 功能描述：MES 系统接口，定义从制造执行系统获取生产数据与用户鉴权的契约。
//           1) GetMaterialNumberAsync：按制令单号查询软件物料号。
//           2) CheckUserInfoAsync：调用 MES 校验用户登录（对应 AutoFrock 的
//              MESMethod.CheckUserInfo），登录鉴权改走 MES，不再查询本地数据库。
//           实现类位于 Infrastructure 层，通过 HTTP 调用 MES 接口交互。
// =============================================================================
namespace IMDP.Domain.Interfaces;

/// <summary>
/// MES 用户校验结果
/// <para>对应 AutoFrock MESMethod.CheckUserInfo 的返回值：
/// <see cref="Success"/> 表示 iRuResult==1（校验通过），
/// <see cref="ErrorInfo"/> 为失败时的错误信息（界面应展示）。</para>
/// </summary>
public class MesCheckUserResult
{
    /// <summary>是否校验通过（对应 AutoFrock iRuResult == 1）</summary>
    public bool Success { get; set; }

    /// <summary>错误信息（对应 AutoFrock _strErrorInfo；校验失败时界面应展示）</summary>
    public string? ErrorInfo { get; set; }
}

/// <summary>
/// MES 产品信息查询结果
/// <para>对应 AutoFrock MESMethod.GetProductInfo / GetProductInfo2 的返回：
/// <see cref="Success"/> 表示 iRuResult==1（符合工序），
/// <see cref="ProductInfo"/> / <see cref="TestClassinfo"/> 为 MES 下发的产品信息与测试类信息。</para>
/// </summary>
public class MesProductInfoResult
{
    /// <summary>是否符合工序（对应 AutoFrock iRuResult == 1）</summary>
    public bool Success { get; set; }

    /// <summary>错误信息（对应 AutoFrock _strErrorInfo）</summary>
    public string? ErrorInfo { get; set; }

    /// <summary>产品信息（对应 AutoFrock _strProductInfo）</summary>
    public string? ProductInfo { get; set; }

    /// <summary>测试类信息（对应 AutoFrock _strTestClassinfo）</summary>
    public string? TestClassinfo { get; set; }
}

/// <summary>
/// MES 通用操作结果
/// <para>用于 SaveProcedure / SaveProcedure2 / SaveTestRecord 等写操作，
/// <see cref="Success"/> 表示 iRuResult==1（MES 接收成功）。</para>
/// </summary>
public class MesOperationResult
{
    /// <summary>操作是否成功（对应 AutoFrock iRuResult == 1）</summary>
    public bool Success { get; set; }

    /// <summary>错误信息（对应 AutoFrock _strErrorInfo）</summary>
    public string? ErrorInfo { get; set; }
}

/// <summary>
/// MES 测试数据查询结果
/// <para>对应 AutoFrock MESMethod.GetProductTestData / GetProductTestData_LastNotNull：
/// <see cref="TestData"/> 为按传入测试项编码顺序回填的测试数据列表。</para>
/// </summary>
public class MesTestDataResult
{
    /// <summary>查询是否成功（对应 AutoFrock iRuResult == 1）</summary>
    public bool Success { get; set; }

    /// <summary>错误信息（对应 AutoFrock _strErrorInfo）</summary>
    public string? ErrorInfo { get; set; }

    /// <summary>测试数据列表（对应 AutoFrock _strTestData[]）</summary>
    public IReadOnlyList<string> TestData { get; set; } = Array.Empty<string>();
}

/// <summary>
/// MES 1对多测试结果查询结果
/// <para>对应 AutoFrock MESMethod.GetTestResult_1N：
/// <see cref="Items"/> 为键值对（键=测试项目类型 cs_Class[i]，值=测试数据 cs_Data[i]），
/// 同一项目类型出现多次时仅保留首个（对应 AutoFrock _mapItemData.Lookup 取首逻辑）。</para>
/// </summary>
public class MesTestResult1NResult
{
    /// <summary>查询是否成功（对应 AutoFrock iRuResult == 1）</summary>
    public bool Success { get; set; }

    /// <summary>错误信息（对应 AutoFrock _strErrorInfo）</summary>
    public string? ErrorInfo { get; set; }

    /// <summary>测试项目类型→测试数据 映射（对应 AutoFrock _mapItemData）</summary>
    public IReadOnlyDictionary<string, string> Items { get; set; } = new Dictionary<string, string>();
}

/// <summary>
/// 制令单软件清单单项
/// <para>对应「制令单软件物料号信息查询」接口 ProductInfo.softwareList[] 元素：
/// <see cref="SoftItemCode"/> 为软件物料号，<see cref="MainBoardCode"/> 为主控板物料编码。</para>
/// </summary>
public class MesMoSoftwareItem
{
    /// <summary>软件物料号（对应接口 softItemCode）</summary>
    public string? SoftItemCode { get; set; }

    /// <summary>主控板物料编码（对应接口 mainBoardCode）</summary>
    public string? MainBoardCode { get; set; }
}

/// <summary>
/// 制令单产品信息
/// <para>对应「制令单软件物料号信息查询」接口的 ProductInfo 对象。</para>
/// </summary>
public class MesMoProductInfo
{
    /// <summary>计划数量（对应接口 planQty）</summary>
    public int PlanQty { get; set; }

    /// <summary>产品编码（对应接口 productCode）</summary>
    public string? ProductCode { get; set; }

    /// <summary>制令单号（对应接口 moNumber）</summary>
    public string? MoNumber { get; set; }

    /// <summary>客户名称（对应接口 custName）</summary>
    public string? CustName { get; set; }

    /// <summary>客户编码（对应接口 custCode）</summary>
    public string? CustCode { get; set; }

    /// <summary>产品名称（对应接口 productName）</summary>
    public string? ProductName { get; set; }

    /// <summary>产品规格，逗号分隔多项特征（对应接口 productSpec）</summary>
    public string? ProductSpec { get; set; }

    /// <summary>软件/主板清单（对应接口 softwareList）</summary>
    public IReadOnlyList<MesMoSoftwareItem> SoftwareList { get; set; } = Array.Empty<MesMoSoftwareItem>();
}

/// <summary>
/// 制令单软件物料号信息查询结果
/// <para>对应「制令单软件物料号信息查询」接口返回：
/// <see cref="Success"/> 表示 RuResult==1（查询成功），<see cref="ProductInfo"/> 为产品信息。</para>
/// </summary>
public class MesProductInfoByMoResult
{
    /// <summary>查询是否成功（对应接口 RuResult == 1）</summary>
    public bool Success { get; set; }

    /// <summary>错误信息（对应接口 ErrorInfo；失败时界面应展示）</summary>
    public string? ErrorInfo { get; set; }

    /// <summary>产品信息（成功时非空）</summary>
    public MesMoProductInfo? ProductInfo { get; set; }
}

/// <summary>
/// MES 系统接口
/// <para>封装与 MES（制造执行系统）的通信，提供制令单-物料号查询与用户登录鉴权等能力。</para>
/// <para>实现类位于 Infrastructure 层，通过 HTTP 调用 MES 接口交互。</para>
/// </summary>
public interface IMesService
{
    /// <summary>
    /// 根据制令单号从 MES 获取对应的软件物料号
    /// <para>内部委托 <see cref="GetProductInfoByMoAsync"/> 查询完整产品信息，取软件清单首个
    /// <see cref="MesMoSoftwareItem.SoftItemCode"/> 作为软件物料号返回。
    /// 查询失败或清单为空时返回 null。</para>
    /// </summary>
    /// <param name="orderNumber">制令单号（对应接口 moNumber）</param>
    /// <returns>软件物料号；MES 中无对应记录时返回 null</returns>
    Task<string?> GetMaterialNumberAsync(string orderNumber);

    /// <summary>
    /// 按制令单号查询产品信息（对应「制令单软件物料号信息查询」HTTP 接口）。
    /// <para>返回产品编码、客户编码/名称、产品名、规格、计划数量及软件/主板清单。</para>
    /// </summary>
    /// <param name="moNumber">制令单号（如 notice2-030-01）</param>
    /// <returns>查询结果；<see cref="MesProductInfoByMoResult.Success"/> 为 true 表示查询成功</returns>
    Task<MesProductInfoByMoResult> GetProductInfoByMoAsync(string moNumber);

    /// <summary>
    /// 调用 MES 校验用户登录（对应 AutoFrock MESMethod.CheckUserInfo）。
    /// <para>登录鉴权改走 MES，不再查询本地数据库校验密码。</para>
    /// </summary>
    /// <param name="userCode">工号（对应 AutoFrock _strUserCode）</param>
    /// <param name="password">密码（明文，对应 AutoFrock _strPWord）</param>
    /// <param name="resCode">资源编码（对应 AutoFrock _strResCode，通常由工位号映射）</param>
    /// <returns>校验结果；<see cref="MesCheckUserResult.Success"/> 为 true 表示登录允许</returns>
    Task<MesCheckUserResult> CheckUserInfoAsync(string userCode, string password, string resCode);

    /// <summary>
    /// 获取产品信息（对应 AutoFrock MESMethod.GetProductInfo）。
    /// <para>按产品序列号 + 工位校验是否符合工序，并下产品信息与测试类信息。</para>
    /// </summary>
    /// <param name="proBar">产品序列号（对应 AutoFrock _strProBar）</param>
    /// <param name="resCode">资源编码/工位（对应 AutoFrock _strResCode）</param>
    /// <returns>查询结果；<see cref="MesProductInfoResult.Success"/> 为 true 表示符合工序</returns>
    Task<MesProductInfoResult> GetProductInfoAsync(string proBar, string resCode);

    /// <summary>
    /// 获取产品信息（带制令单号，对应 AutoFrock MESMethod.GetProductInfo2）。
    /// </summary>
    /// <param name="assyMoNumber">制令单号（对应 AutoFrock sAssyMoNumber）</param>
    /// <param name="proBar">产品序列号</param>
    /// <param name="resCode">资源编码/工位</param>
    Task<MesProductInfoResult> GetProductInfo2Async(string assyMoNumber, string proBar, string resCode);

    /// <summary>
    /// 保存工序结果到 MES（对应 AutoFrock MESMethod.SaveProcedure）。
    /// </summary>
    /// <param name="proBar">产品序列号</param>
    /// <param name="result">检测结果（0=不合格/维修，1=合格，对应 AutoFrock _iResult）</param>
    /// <param name="userCode">操作员工号</param>
    /// <param name="resCode">工位</param>
    /// <param name="testResult">测试结果串（对应 AutoFrock _strTestResult）</param>
    /// <param name="errCode">错误码串（对应 AutoFrock _strErrCode）</param>
    Task<MesOperationResult> SaveProcedureAsync(string proBar, int result, string userCode, string resCode, string testResult, string errCode);

    /// <summary>
    /// 保存工序结果到 MES（带制令单号，对应 AutoFrock MESMethod.SaveProcedure2）。
    /// </summary>
    /// <param name="assyMoNumber">制令单号</param>
    /// <param name="proBar">产品序列号</param>
    /// <param name="result">检测结果（0=不合格，1=合格）</param>
    /// <param name="userCode">操作员工号</param>
    /// <param name="resCode">工位</param>
    /// <param name="testResult">测试结果串</param>
    /// <param name="errCode">错误码串</param>
    Task<MesOperationResult> SaveProcedure2Async(string assyMoNumber, string proBar, int result, string userCode, string resCode, string testResult, string errCode);

    /// <summary>
    /// 批量获取产品测试数据（对应 AutoFrock MESMethod.GetProductTestData，底层 EqOperation.GetTestResult）。
    /// </summary>
    /// <param name="proBar">产品序列号/表号</param>
    /// <param name="testCodes">测试项编码列表（对应 AutoFrock _strTestCode[]，数量 Num）</param>
    /// <returns>查询结果；<see cref="MesTestDataResult.TestData"/> 按传入编码顺序回填</returns>
    Task<MesTestDataResult> GetProductTestDataAsync(string proBar, IReadOnlyList<string> testCodes);

    /// <summary>
    /// 获取产品最近一次非空测试数据（对应 AutoFrock MESMethod.GetProductTestData_LastNotNull）。
    /// </summary>
    /// <param name="proBar">产品序列号/表号</param>
    /// <param name="testCode">测试项编码</param>
    Task<MesTestDataResult> GetProductTestDataLastNotNullAsync(string proBar, string testCode);

    /// <summary>
    /// 获取一项多值测试结果（对应 AutoFrock MESMethod.GetTestResult_1N）。
    /// <para>按表号 + 项目编码查询该项目下所有测试类型与数据，组装为键值对。</para>
    /// </summary>
    /// <param name="meterCode">表号</param>
    /// <param name="itemCode">项目编码（如 "C-BJXBWC"）</param>
    Task<MesTestResult1NResult> GetTestResult1NAsync(string meterCode, string itemCode);

    /// <summary>
    /// 保存测试记录到 MES（对应 AutoFrock MESMethod.SaveTestRecord）。
    /// <para>底层为 EqOperation.SaveTestRecord，固定 20 个 record 槽位；
    /// <paramref name="records"/> 不足 20 个时尾部补空串（对应 AutoFrock 拆串后补齐逻辑）。</para>
    /// </summary>
    /// <param name="proBar">产品序列号</param>
    /// <param name="result">检测结果（0=不合格，1=合格）</param>
    /// <param name="userCode">操作员工号</param>
    /// <param name="resCode">工位</param>
    /// <param name="records">测试记录列表（对应 AutoFrock 按 '|' 拆分后的 _arrayRecord）</param>
    Task<MesOperationResult> SaveTestRecordAsync(string proBar, int result, string userCode, string resCode, IReadOnlyList<string> records);
}
