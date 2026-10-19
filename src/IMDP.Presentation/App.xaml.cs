// =============================================================================
// 文件名：App.xaml.cs
// 功能描述：IMDP 智能检测管理平台应用程序入口。
//           负责 Prism 应用引导：DI 容器注册、模块目录配置、数据库初始化，
//           以及将登录视图注册到主内容区域。
// =============================================================================
using System.IO;
using System.Windows;
using DryIoc;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using IMDP.Domain.Interfaces;
using IMDP.Infrastructure.Persistence;
using IMDP.Infrastructure.Serial;
using IMDP.Infrastructure.Protocol;
using IMDP.Infrastructure.Services;
using IMDP.Application.Services;
using IMDP.Presentation.Services;
using IMDP.Presentation.Views;
using IMDP.Presentation.ViewModels;
using NLog;

using IMDP.Infrastructure.Logging;
namespace IMDP.Presentation;

/// <summary>
/// IMDP 应用程序主入口
/// <para>继承 <see cref="PrismApplication"/>（DryIoc 容器），
/// 负责创建主窗口、注册依赖项、加载产品插件模块并初始化数据库。</para>
/// </summary>
public partial class App : PrismApplication
{
    private static readonly NLog.ILogger _logger = LogHelper.For<App>();

    /// <summary>
    /// 创建主窗口（Shell）
    /// </summary>
    /// <returns>主窗口 <see cref="MainWindow"/> 实例</returns>
    protected override Window CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    /// <summary>
    /// 注册依赖项
    /// <para>统一注册仓储、应用服务、产品插件基础设施、对话框视图及导航视图。</para>
    /// </summary>
    /// <param name="containerRegistry">Prism 容器注册器</param>
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        var config = LoadAppConfig();
        var connectionString = config.ConnectionString;

        // ===== 当前用户上下文（共享单例，存放登录用户信息） =====
        containerRegistry.RegisterSingleton<ICurrentContext, CurrentContext>();
        // 数据库上下文（SqlSugar 单例，注入连接串与当前用户上下文）
        containerRegistry.RegisterSingleton<IDbContext>(() =>
            new SqlSugarDbContext(connectionString, Container.Resolve<ICurrentContext>(), config.Security.DefaultAdminPassword));

        // ===== 串口与协议基础设施（单例） =====
        containerRegistry.RegisterSingleton<ISerialService, SerialService>();
        containerRegistry.RegisterSingleton<IProtocolManager, ProtocolManager>();
        containerRegistry.RegisterSingleton<IHttpClientService, HttpClientService>();

        // ===== 仓储自动注册（反射扫描 Infrastructure 程序集，自动注册所有仓储接口到实现） =====
        AutoRegisterRepositories(containerRegistry);

        // ===== 审计日志仓储手动注册（不继承 IRepository<T>，无法被 AutoRegisterRepositories 反射扫描到） =====
        containerRegistry.Register<IBoardConfigAuditLogRepository, BoardConfigAuditLogRepository>();  // 板卡配置变更审计日志仓储
        containerRegistry.Register<IWholeConfigAuditLogRepository, WholeConfigAuditLogRepository>();  // 整机配置变更审计日志仓储

        // ===== 应用服务（瞬态） =====
        containerRegistry.Register<IUserService, UserService>();                    // 用户管理服务
        // MES 服务（登录鉴权走 MES；接口 URL 由 appsettings.json Mes:Mo 注入）
        containerRegistry.Register<IMesService>(() => new MesService(config.Mes.Mo.Url, Container.Resolve<IHttpClientService>()));
        containerRegistry.Register<IGlobalConfigService, GlobalConfigService>();    // 全局配置服务
        containerRegistry.Register<ITestPlanService, TestPlanService>();            // 测试计划服务
        containerRegistry.Register<ITestRecordService, TestRecordService>();        // 测试记录服务
        containerRegistry.Register<IStationService, StationService>();              // 工位服务
        containerRegistry.Register<IBoardDetectService, BoardDetectService>();      // 板卡检测服务
        // 应用更新服务（依赖 appsettings.json 的 Update 段，由工厂注入）
        containerRegistry.Register<IUpdateService>(() => new UpdateService(
            Container.Resolve<IAppUpdateRecordRepository>(),
            config.Update.UpdateXmlUrl,
            config.Update.SkipUpdateKey,
            Container.Resolve<IHttpClientService>()));
        containerRegistry.Register<ILogQueryService, LogQueryService>();            // 日志查询服务（登录/升级/工单切换）
        containerRegistry.RegisterSingleton<IOrderSwitchRecordService, OrderSwitchRecordService>(); // 工单切换记录写入与会话去重

        // ===== 产品插件基础设施（单例） =====
        containerRegistry.RegisterSingleton<IProductPluginRegistry, ProductPluginRegistry>();                    // 产品插件注册表
        containerRegistry.RegisterSingleton<IProductPluginNavigationService, ProductPluginNavigationService>();  // 产品插件导航服务

        // ===== 统一消息提示服务（收敛散落的 MessageBox.Show；线程安全；错误自动记日志） =====
        containerRegistry.RegisterSingleton<IMessageService, MessageService>();

        // 注册对话框视图（Window 类型，用于 Container.Resolve 显示）
        containerRegistry.Register<SettingsDialogView>();
        containerRegistry.Register<UpdateWindow>();   // 升级更新窗口（4.2）

        // ===== 导航视图注册（View + ViewModel 配对，用于 Prism 区域导航） =====
        containerRegistry.RegisterForNavigation<MainView, MainViewModel>();                                  // 主视图
        containerRegistry.RegisterForNavigation<LoginView, LoginViewModel>();                                // 登录视图
        containerRegistry.RegisterForNavigation<UserManageView, UserManageViewModel>();                      // 用户管理视图
        containerRegistry.RegisterForNavigation<MaterialProductMapView, MaterialProductMapViewModel>();      // 物料-产品映射视图
        containerRegistry.RegisterForNavigation<BoardConfigHistoryView, BoardConfigHistoryViewModel>();      // 板卡配置历史视图
        containerRegistry.RegisterForNavigation<WholeConfigHistoryView, WholeConfigHistoryViewModel>();      // 整机配置历史视图
        containerRegistry.RegisterForNavigation<BoardQueryView, BoardQueryViewModel>();                      // 板卡查询视图
        containerRegistry.RegisterForNavigation<WholeQueryView, WholeQueryViewModel>();                      // 整机查询视图
        containerRegistry.RegisterForNavigation<VersionView, VersionViewModel>();                            // 版本信息视图
        containerRegistry.RegisterForNavigation<LoginQueryView, LoginQueryViewModel>();                      // 登录日志查询视图（4.15）
        containerRegistry.RegisterForNavigation<UpdateQueryView, UpdateQueryViewModel>();                    // 升级日志查询视图（4.16）
        containerRegistry.RegisterForNavigation<OrderSwitchQueryView, OrderSwitchQueryViewModel>();          // 工单切换日志查询视图（4.17）
        containerRegistry.RegisterForNavigation<UpdateWindow, UpdateViewModel>();                            // 更新界面视图与对应模型(8.4)
    }

    /// <summary>
    /// 自动注册仓储
    /// <para>反射扫描 Infrastructure 程序集，找到所有继承自 <see cref="IRepository{T}"/> 的仓储接口及其实现类，
    /// 自动注册到 DI 容器（瞬态生命周期）。新增产品拆分表时无需修改此方法。</para>
    /// </summary>
    /// <param name="containerRegistry">Prism 容器注册器</param>
    private static void AutoRegisterRepositories(IContainerRegistry containerRegistry)
    {
        var infrastructureAssembly = typeof(SqlSugarDbContext).Assembly;

        foreach (var implType in infrastructureAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && !t.IsGenericType))
        {
            // 查找该类实现的仓储接口：继承自 IRepository<T> 的非泛型接口
            var repoInterfaces = implType.GetInterfaces()
                .Where(i => i.IsInterface
                         && !i.IsGenericType
                         && i.GetInterfaces().Any(baseI =>
                             baseI.IsGenericType && baseI.GetGenericTypeDefinition() == typeof(IRepository<>)));

            foreach (var interfaceType in repoInterfaces)
            {
                containerRegistry.Register(interfaceType, implType);
            }
        }
    }

    /// <summary>
    /// 创建模块目录
    /// <para>使用目录扫描方式动态加载应用程序基目录下的产品插件 DLL。</para>
    /// </summary>
    /// <returns>目录模块目录实例</returns>
    protected override IModuleCatalog CreateModuleCatalog()
    {
        // 动态扫描应用程序基目录下的产品插件 DLL，发现并加载所有 IModule 实现
        // 共享层不依赖产品插件，由 Prism 在运行时通过反射加载
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        return new Prism.Modularity.DirectoryModuleCatalog { ModulePath = baseDir };
    }

    /// <summary>
    /// 应用程序初始化完成后的处理
    /// <para>立即将登录视图注册到主内容区域（登录界面先渲染），随后后台执行
    /// 数据库建表与种子数据初始化、version.xml 同步及启动更新检测，不阻塞登录界面。</para>
    /// </summary>
    protected override void OnInitialized()
    {
        base.OnInitialized();

        // ===== 临时测试：调用 MES GetMaterialNumberAsync 接口，验证制令单查询链路 =====
        //try
        //{
        //    var mes = Container.Resolve<IMesService>();
        //    var materialNo = await mes.GetMaterialNumberAsync("notice2-030-01");
        //    _logger.Info($"[MES 测试] GetMaterialNumberAsync(notice2-030-01) 返回：{materialNo ?? "(null)"}");
        //}
        //catch (Exception ex)
        //{
        //    _logger.Error(ex, "[MES 测试] GetMaterialNumberAsync 调用异常");
        //}

        // 将登录视图注册到主内容区域，启动后默认显示登录界面。
        // 先注册登录视图再后台执行数据库初始化/版本同步/更新检测，
        // 避免数据库或网络缓慢时登录界面迟迟渲染不出来（此前这些 IO 全部排在注册登录视图之前，串行等待）
        var regionManager = Container.Resolve<IRegionManager>();
        regionManager.RegisterViewWithRegion("MainContentRegion", typeof(LoginView));

        // 启动后台任务链：数据库初始化 → 版本信息同步 → 启动更新检测（不阻塞登录界面渲染，失败仅记日志）
        _ = RunStartupTasksAsync();
    }

    /// <summary>
    /// 启动后台任务：数据库初始化 → 本地 version.xml 同步 → 启动更新检测。
    /// <para>各步逐段捕获异常仅记日志，任何一步失败都不影响登录界面使用；
    /// 数据库尚未就绪期间的登录日志写入由 RecordLoginAsync 自身容错（全 catch 静默返回 0，不阻断登录）。</para>
    /// </summary>
    private async Task RunStartupTasksAsync()
    {
        try
        {
            // 创建数据库表结构并写入种子数据
            // CodeFirst 已开启 IsAutoUpdateTable：
            //   - 新建不存在的表（如 LoginConfig）
            //   - 补齐已存在表中缺失的列（如旧 Logins 表缺 IpAddress/LoginType/LoginResult/ErrorInfo）
            var dbContext = Container.Resolve<IDbContext>();
            await dbContext.EnsureCreatedAsync();
            await dbContext.SeedDataAsync();
            _logger.Info("数据库初始化完成");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "数据库初始化失败");
        }

        // 启动时将代码硬编码中定义的最新版本及更新说明同步至本地 version.xml（自动归档旧版本至 history）。
        // 基底按"服务器优先、本地回退"加载：先获取更新服务器上的 version.xml（权威数据源、history 最全），
        // 服务器没有时回退本地文件，两者均无则全新生成；仅追加更高版本，不降级覆盖。
        try
        {
            var updateService = Container.Resolve<IUpdateService>();
            await updateService.SyncCodeVersionToXmlAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "版本信息同步至 version.xml 失败");
        }

        // 启动时自动检测新版本（由 appsettings.json Update:CheckOnStartup 控制，默认开启）
        var config = LoadAppConfig();
        if (config.Update.CheckOnStartup)
        {
            await CheckForUpdateAsync(showWhenNoUpdate: false);
        }

        // 公共协议工具类自测（仅 DEBUG 编译；延迟 5 秒后台执行，结果写 NLog，不弹窗不阻塞）
        //_ = CommonServiceSelfTest.RunAfterStartupAsync();
    }

    /// <summary>
    /// 应用程序退出处理
    /// <para>根据配置在退出时自动检测新版本（若有则弹出升级更新窗口供用户选择更新或暂不更新）。</para>
    /// </summary>
    /// <param name="e">退出事件参数</param>
    protected override async void OnExit(ExitEventArgs e)
    {
        var config = LoadAppConfig();
        if (config.Update.CheckOnExit)
        {
            // 退出时检测到新版本则弹窗询问；用户选择暂不更新或立即更新后继续退出流程
            await CheckForUpdateAsync(showWhenNoUpdate: false);
        }
        base.OnExit(e);
    }

    /// <summary>
    /// 检查更新并弹岀升级更新窗口
    /// </summary>
    /// <param name="showWhenNoUpdate">无新版本时是否仍弹窗（手动触发时为 true，自动检测时为 false）</param>
    private async Task CheckForUpdateAsync(bool showWhenNoUpdate)
    {
        try
        {
            var updateService = Container.Resolve<IUpdateService>();
            var versionInfo = await updateService.CheckUpdateAsync();
            if (versionInfo == null && !showWhenNoUpdate)
                return;

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var window = Container.Resolve<UpdateWindow>();
                window.Owner = System.Windows.Application.Current.MainWindow;
                if (versionInfo != null)
                {
                    // 将检测到的版本信息预填充至 UpdateViewModel
                    if (window.DataContext is UpdateViewModel vm)
                    {
                        vm.ApplyDetectedVersion(versionInfo);
                    }
                }
                window.ShowDialog();
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "检查更新失败");
        }
    }

    /// <summary>
    /// 加载应用配置
    /// <para>从 appsettings.json 读取连接串、安全配置等；若文件缺失或解析失败则抛异常。</para>
    /// </summary>
    /// <returns>应用配置实例</returns>
    /// <exception cref="InvalidOperationException">配置文件缺失或连接串为空时抛出</exception>
    private AppConfig LoadAppConfig()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath))
            throw new InvalidOperationException($"配置文件不存在：{configPath}");

        var json = File.ReadAllText(configPath);
        var config = Newtonsoft.Json.JsonConvert.DeserializeObject<AppConfig>(json)
            ?? throw new InvalidOperationException("配置文件解析失败");

        if (string.IsNullOrEmpty(config.ConnectionString))
            throw new InvalidOperationException("配置文件中 ConnectionString 不能为空");

        return config;
    }

    /// <summary>
    /// 应用配置内部模型
    /// <para>用于反序列化 appsettings.json。</para>
    /// </summary>
    private class AppConfig
    {
        /// <summary>数据库连接字符串</summary>
        public string ConnectionString { get; set; } = string.Empty;
        /// <summary>安全配置</summary>
        public SecurityConfig Security { get; set; } = new();
        /// <summary>应用更新配置（IIS 更新服务器地址、免更新密钥、检测开关）</summary>
        public UpdateConfig Update { get; set; } = new();
        /// <summary>MES 配置（接口 URL，供 MesService 使用）</summary>
        public MesConfig Mes { get; set; } = new();
    }

    /// <summary>
    /// MES 配置
    /// <para>对应 appsettings.json 的 Mes 段。</para>
    /// </summary>
    private class MesConfig
    {
        /// <summary>制令单查询接口配置（GetProductInfoByMoAsync 的 URL）</summary>
        public MoConfig Mo { get; set; } = new();
    }

    /// <summary>
    /// 制令单查询接口配置
    /// <para>对应 appsettings.json 的 Mes:Mo 段。</para>
    /// </summary>
    private class MoConfig
    {
        /// <summary>制令单软件物料号信息查询接口 URL</summary>
        public string Url { get; set; } = string.Empty;
    }

    /// <summary>安全配置</summary>
    private class SecurityConfig
    {
        /// <summary>默认管理员密码（用于首次启动种子数据，为空则不创建默认管理员）</summary>
        public string DefaultAdminPassword { get; set; } = string.Empty;
    }

    /// <summary>
    /// 应用更新配置
    /// <para>对应 appsettings.json 的 Update 段，定义 IIS 更新服务器地址、免更新密钥与检测开关。</para>
    /// </summary>
    private class UpdateConfig
    {
        /// <summary>更新清单 version.xml 的 URL（内网 IIS 静态文件地址）</summary>
        public string UpdateXmlUrl { get; set; } = string.Empty;

        /// <summary>免更新密钥（用户选择"暂不更新"时需输入此密钥方可跳过本次更新）</summary>
        public string SkipUpdateKey { get; set; } = string.Empty;

        /// <summary>是否在应用启动时自动检测新版本</summary>
        public bool CheckOnStartup { get; set; } = true;

        /// <summary>是否在应用退出时自动检测新版本</summary>
        public bool CheckOnExit { get; set; } = true;
    }
}
