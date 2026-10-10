// =============================================================================
// 文件名：SqlSugarDbContext.cs
// 功能描述：SqlSugar 数据库上下文实现
//           负责初始化 SqlSugarScope 客户端、配置审计 AOP（自动填充
//           ComputerName/UserName/AppName）、Code-First 建表、索引创建与种子数据初始化。
// =============================================================================
using Microsoft.Data.SqlClient;
using SqlSugar;
using IMDP.Domain.Entities;
using IMDP.Domain.Interfaces;
using IMDP.Domain.Users;

namespace IMDP.Infrastructure.Persistence;

/// <summary>
/// SqlSugar 数据库上下文
/// <para>实现 <see cref="IDbContext"/>，对外提供 <see cref="ISqlSugarClient"/>；
/// 内部使用 <see cref="SqlSugarScope"/> 作为单例作用域客户端，并配置审计字段 AOP。</para>
/// </summary>
public class SqlSugarDbContext : IDbContext
{
    private readonly string _connectionString;
    private readonly SqlSugarScope _db;
    private readonly ICurrentContext _currentContext;
    private readonly string _defaultAdminPassword;

    /// <summary>获取 SqlSugar 客户端实例。</summary>
    public ISqlSugarClient Client => _db;

    /// <summary>
    /// 初始化 <see cref="SqlSugarDbContext"/> 类的新实例。
    /// </summary>
    /// <param name="connectionString">数据库连接字符串。</param>
    /// <param name="currentContext">当前上下文，用于审计字段填充。</param>
    /// <param name="defaultAdminPassword">默认管理员密码（用于首次启动种子数据，为空则不创建默认管理员）。</param>
    public SqlSugarDbContext(string connectionString, ICurrentContext currentContext, string defaultAdminPassword = "")
    {
        _connectionString = connectionString;
        _currentContext = currentContext;
        _defaultAdminPassword = defaultAdminPassword ?? string.Empty;
        // 创建 SqlSugarScope（单例作用域），并配置 SQL Server 与自动关闭连接
        _db = new SqlSugarScope(new ConnectionConfig()
        {
            ConnectionString = _connectionString,
            DbType = DbType.SqlServer,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });

        // 配置审计 AOP，自动填充审计字段
        ConfigureAuditAop();
    }

    /// <summary>
    /// 配置 SqlSugar AOP 拦截，在插入和更新时自动填充 ComputerName/UserName/AppName 审计字段。
    /// </summary>
    private void ConfigureAuditAop()
    {
        _db.Aop.DataExecuting = (oldValue, entityInfo) =>
        {
            // 仅对基于对象的插入与更新操作进行拦截
            if (entityInfo.OperationType is DataFilterType.InsertByObject
                or DataFilterType.UpdateByObject)
            {
                switch (entityInfo.PropertyName)
                {
                    case nameof(IAuditable.ComputerName):
                        entityInfo.SetValue(_currentContext.ComputerName);
                        break;
                    case nameof(IAuditable.UserName):
                        entityInfo.SetValue(Environment.UserName);  //用的window用户名
                        break;
                    case nameof(IAuditable.AppName):
                        entityInfo.SetValue(_currentContext.AppName);
                        break;
                }
            }
        };
    }

    /// <summary>
    /// 确保数据库表与索引已创建（Code-First 模式）。
    /// <para>先确保数据库本身存在（不存在时自动创建），再使用 SqlSugar 的
    /// CodeFirst.InitTables 初始化所有实体表；随后通过 T-SQL 创建关键索引以提升查询性能。</para>
    /// </summary>
    public async Task EnsureCreatedAsync()
    {
        // 数据库不存在时先自动创建（SqlSugar CodeFirst 只建表不建库）
        await EnsureDatabaseExistsAsync();

        // Code-First 模式初始化普通实体表（非分表）
        //  - 表不存在 → 自动 CREATE TABLE（包含 LoginConfig 新表）
        //  - 表已存在 → 跳过不修改，避免 SqlSugar 误删列或改数据类型
        _db.CodeFirst.InitTables(
            typeof(User),
            typeof(MaterialProductMap),
            typeof(Login),
            typeof(LoginConfig),
            typeof(OrderSwitch),
            typeof(GlobalConfig),
            typeof(AppUpdateRecord)
        );

        // 补齐 Logins 表中后增但旧版数据库可能缺失的 4 个列。
        // 使用 IF COL_LENGTH(...) IS NULL 包裹，确保列不存在时才 ALTER TABLE ADD，
        // 幂等可重复执行，不影响已有列与数据。
        await EnsureLoginsTableColumnsAsync();

        // 创建分表物理表（4 个产品类别 × 6 个分表实体 = 24 张物理表）
        CreateSplitTables();

        await CreateIndexesAsync();
    }

    /// <summary>
    /// 确保连接字符串指向的数据库存在，不存在时自动创建。
    /// <para>SqlSugar 的 CodeFirst.InitTables 只负责建表，不创建数据库；
    /// 此方法从连接字符串解析目标库名，使用独立的 master 连接判断库是否存在，
    /// 不存在则 CREATE DATABASE 后等待其可用，全程幂等可重复执行。</para>
    /// </summary>
    private async Task EnsureDatabaseExistsAsync()
    {
        // 从连接字符串提取 Database=xxx（不区分大小写）
        var databaseName = _connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => part.StartsWith("Database", StringComparison.OrdinalIgnoreCase) ||
                           part.StartsWith("Initial Catalog", StringComparison.OrdinalIgnoreCase))
            .Select(part => part.Substring(part.IndexOf('=') + 1).Trim())
            .FirstOrDefault();

        // 未配置 Database 时无法判断，交由后续连接流程报错
        if (string.IsNullOrEmpty(databaseName))
        {
            return;
        }

        // 始终使用 master 库连接判断与建库（目标库尚不存在时，指向目标库的连接会直接失败）
        var builder = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = "master" };
        await using var masterConnection = new SqlConnection(builder.ConnectionString);
        await masterConnection.OpenAsync();

        // 判断目标数据库是否已存在（参数化查询避免拼接）
        using (var existsCommand = masterConnection.CreateCommand())
        {
            existsCommand.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @name";
            var parameter = existsCommand.CreateParameter();
            parameter.ParameterName = "@name";
            parameter.Value = databaseName;
            existsCommand.Parameters.Add(parameter);
            var count = Convert.ToInt32(await existsCommand.ExecuteScalarAsync());
            if (count > 0)
            {
                return;
            }
        }

        // 库名含特殊字符时按标识符规则包裹（正常配置不会触发）
        var escapedName = System.Text.RegularExpressions.Regex.IsMatch(databaseName, @"^[A-Za-z][A-Za-z0-9_]*$")
            ? databaseName
            : $"[{databaseName.Replace("[", "").Replace("]", "")}]";

        // 执行建库
        using (var createCommand = masterConnection.CreateCommand())
        {
            createCommand.CommandText = $"CREATE DATABASE {escapedName}";
            await createCommand.ExecuteNonQueryAsync();
        }

        // 等待新库进入 ONLINE 状态（避免建库后立即连接触发尚未就绪）
        using var checkCommand = masterConnection.CreateCommand();
        checkCommand.CommandText = "SELECT state_desc FROM sys.databases WHERE name = @name";
        var checkParameter = checkCommand.CreateParameter();
        checkParameter.ParameterName = "@name";
        checkParameter.Value = databaseName;
        checkCommand.Parameters.Add(checkParameter);
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var state = await checkCommand.ExecuteScalarAsync() as string;
            if (state == "ONLINE")
            {
                return;
            }

            await Task.Delay(200);
        }
    }

    /// <summary>
    /// 安全补齐 Logins 表缺失的后增列（IpAddress / LoginType / LoginResult / ErrorInfo）。
    /// <para>仅在列不存在时执行 ALTER TABLE ADD，幂等执行不报错，不修改已有列或数据。</para>
    /// </summary>
    private async Task EnsureLoginsTableColumnsAsync()
    {
        var sql = new System.Text.StringBuilder();

        // IpAddress NVARCHAR(255) NULL
        sql.Append("IF OBJECT_ID('dbo.LoginRecords', 'U') IS NOT NULL AND COL_LENGTH('dbo.LoginRecords', 'IpAddress') IS NULL ");
        sql.Append("ALTER TABLE dbo.LoginRecords ADD IpAddress NVARCHAR(255) NULL; ");

        // LoginType NVARCHAR(255) NULL
        sql.Append("IF OBJECT_ID('dbo.LoginRecords', 'U') IS NOT NULL AND COL_LENGTH('dbo.LoginRecords', 'LoginType') IS NULL ");
        sql.Append("ALTER TABLE dbo.LoginRecords ADD LoginType NVARCHAR(255) NULL; ");

        // LoginResult NVARCHAR(255) NULL
        sql.Append("IF OBJECT_ID('dbo.LoginRecords', 'U') IS NOT NULL AND COL_LENGTH('dbo.LoginRecords', 'LoginResult') IS NULL ");
        sql.Append("ALTER TABLE dbo.LoginRecords ADD LoginResult NVARCHAR(255) NULL; ");

        // ErrorInfo NVARCHAR(255) NULL
        sql.Append("IF OBJECT_ID('dbo.LoginRecords', 'U') IS NOT NULL AND COL_LENGTH('dbo.LoginRecords', 'ErrorInfo') IS NULL ");
        sql.Append("ALTER TABLE dbo.LoginRecords ADD ErrorInfo NVARCHAR(255) NULL; ");

        await _db.Ado.ExecuteCommandAsync(sql.ToString());
    }

    /// <summary>
    /// 创建分表物理表。
    /// <para>分表实体（BoardConfig/WholeConfig/Boards/Wholes 及审计日志）使用 AS(tableName)
    /// 动态路由到 {Category}{基表名} 物理表，需按 4 个产品类别分别创建物理表。</para>
    /// <para>通过 MappingTables 临时映射表名后调用 CodeFirst.InitTables 建表，建表后清理映射，
    /// 避免影响后续 AS 动态路由查询。</para>
    /// </summary>
    private void CreateSplitTables()
    {
        // 6 个分表实体类型（类名即基表名，如 BoardConfig → GasDGBoardConfig）
        var splitTableTypes = new[]
        {
            typeof(BoardConfig),
            typeof(WholeConfig),
            typeof(Boards),
            typeof(Wholes),
            typeof(BoardConfigAuditLog),
            typeof(WholeConfigAuditLog)
        };

        // 4 个产品类别前缀
        var categories = new[] { "GasDG", "GasOG", "WaterDW", "WaterOW" };

        foreach (var category in categories)
        {
            foreach (var type in splitTableTypes)
            {
                // 类名即基表名（实体类命名与 [SugarTable] 表名一致）
                var physicalTableName = $"{category}{type.Name}";

                // 临时映射实体名到物理表名后建表（表已存在时 InitTables 自动跳过创建）
                _db.MappingTables.Add(type.Name, physicalTableName);
                _db.CodeFirst.InitTables(type);
            }
        }

        // 清理映射表，避免影响后续 AS 动态路由查询
        _db.MappingTables.Clear();
    }

    /// <summary>
    /// 创建关键索引（仅在不存在时创建），覆盖高频查询字段。
    /// <para>使用循环拼接 T-SQL，避免 4 个产品类别 × 7-8 个索引的重复代码。</para>
    /// </summary>
    private async Task CreateIndexesAsync()
    {
        // 通用表索引（非分表）
        var commonIndexes = new[]
        {
            ("MaterialProductMaps", "IX_MaterialProductMaps_MaterialNumber", "MaterialNumber"),
            ("LoginRecords", "IX_LoginRecords_WorkNumber", "WorkNumber"),
            ("LoginRecords", "IX_LoginRecords_LoginTime", "LoginTime"),
            ("LoginRecords", "IX_LoginRecords_Station", "Station"),
            ("LoginConfig", "IX_LoginConfig_WorkNumber", "WorkNumber"),
            ("LoginConfig", "IX_LoginConfig_LoginTime", "LoginTime"),
            ("LoginConfig", "IX_LoginConfig_Station", "Station"),
            ("OrderSwitchs", "IX_OrderSwitchs_OrderNumber", "OrderNumber"),
            ("OrderSwitchs", "IX_OrderSwitchs_MaterialNumber", "MaterialNumber"),
            ("OrderSwitchs", "IX_OrderSwitchs_CreatedTime", "CreatedTime")
        };

        // 4 个产品类别前缀
        var categories = new[] { "GasDG", "GasOG", "WaterDW", "WaterOW" };

        // 板卡相关表索引定义：(表后缀, 索引名后缀, 字段名)
        var boardIndexes = new[]
        {
            ("BoardConfig", "MaterialNumber", "MaterialNumber"),
            ("Boards", "OrderNumber", "OrderNumber"),
            ("Boards", "ProductSn", "ProductSn"),
            ("Boards", "ProductCode", "ProductCode"),
            ("Boards", "CreatedTime", "CreatedTime"),
            ("BoardConfigAuditLog", "CreatedTime", "CreatedTime")
        };

        // 整机相关表索引定义
        var wholeIndexes = new[]
        {
            ("WholeConfig", "MaterialNumber", "MaterialNumber"),
            ("WholeConfig", "PlanType", "PlanType"),
            ("Wholes", "OrderNumber", "OrderNumber"),
            ("Wholes", "ProductSn", "ProductSn"),
            ("Wholes", "CreatedTime", "CreatedTime"),
            ("Wholes", "TestType", "TestType"),
            ("WholeConfigAuditLog", "CreatedTime", "CreatedTime")
        };

        var sql = new System.Text.StringBuilder();

        // 通用表索引
        foreach (var (table, indexName, column) in commonIndexes)
        {
            sql.Append($"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = '{indexName}' AND object_id = OBJECT_ID('{table}')) ");
            sql.Append($"CREATE INDEX {indexName} ON {table}({column}); ");
        }
        //防止并发
        sql.Append("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_Users_WorkNumber_Active' AND object_id = OBJECT_ID('Users')) ");
        sql.Append("CREATE UNIQUE INDEX UX_Users_WorkNumber_Active ON Users(WorkNumber) WHERE IsDeleted = 0; ");

        // 4 个产品类别的板卡 + 整机索引
        foreach (var cat in categories)
        {
            foreach (var (suffix, indexSuffix, column) in boardIndexes)
            {
                var tableName = $"{cat}{suffix}";
                var indexName = $"IX_{tableName}_{indexSuffix}";
                sql.Append($"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = '{indexName}' AND object_id = OBJECT_ID('{tableName}')) ");
                sql.Append($"CREATE INDEX {indexName} ON {tableName}({column}); ");
            }

            foreach (var (suffix, indexSuffix, column) in wholeIndexes)
            {
                var tableName = $"{cat}{suffix}";
                var indexName = $"IX_{tableName}_{indexSuffix}";
                sql.Append($"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = '{indexName}' AND object_id = OBJECT_ID('{tableName}')) ");
                sql.Append($"CREATE INDEX {indexName} ON {tableName}({column}); ");
            }
        }

        await _db.Ado.ExecuteCommandAsync(sql.ToString());
    }

    /// <summary>
    /// 初始化种子数据。
    /// <para>当用户表为空且配置了默认管理员密码时插入默认管理员账号；当全局配置表为空时插入默认全局配置。</para>
    /// </summary>
    public async Task SeedDataAsync()
    {
        // 用户表为空且配置了默认密码时插入默认管理员
        if (!await _db.Queryable<User>().AnyAsync() && !string.IsNullOrEmpty(_defaultAdminPassword))
        {
            var admin = new User
            {
                WorkNumber = "admin",
                Name = "系统管理员",
                // 密码哈希使用 PBKDF2 加盐算法（与 UserService 保持一致）
                PasswordHash = IMDP.Domain.Services.PasswordHasher.HashPassword(_defaultAdminPassword),
                Role = UserRoleCodes.SystemAdmin,
                CreatedBy = "system",
                CreatedTime = DateTime.Now,
                UpdatedBy = "system",
                UpdatedTime = DateTime.Now
            };
            await _db.Insertable(admin).ExecuteCommandAsync();
        }

        // 全局配置表为空时插入默认配置（串口 9600/N/8/1）
        if (!await _db.Queryable<GlobalConfig>().AnyAsync())
        {
            var config = new GlobalConfig
            {
                Id = 1,
                BaudRate = 9600,
                DataBits = 8,
                StopBits = "One",
                Parity = "None",
                CommandsJson = "[]",
                MaterialNumber = "",
                CreatedBy = "system",
                CreatedTime = DateTime.Now,
                UpdatedBy = "system",
                UpdatedTime = DateTime.Now
            };
            await _db.Insertable(config).ExecuteCommandAsync();
        }
    }
}
