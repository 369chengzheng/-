// =============================================================================
// 文件名：IConfigEntity.cs
// 功能描述：按产品分类拆分的配置/记录实体通用接口。
//           GasDG/GasOG/WaterDW/WaterOW 4 套实体共用同一接口，便于应用层以统一泛型方法
//           处理 MapToDto / ParseAuditLog 等映射逻辑，消除 4 份重复代码。
// =============================================================================
namespace IMDP.Domain.Entities;

/// <summary>板卡配置实体通用接口（4 个产品 BoardConfig 实体共用）</summary>
public interface IBoardConfig : ISoftDeletable
{
    long Id { get; set; }
    string ProductCode { get; set; }
    string MaterialNumber { get; set; }
    string ParametersJson { get; set; }
    int StationCount { get; set; }
    string CreatedBy { get; set; }
    DateTime CreatedTime { get; set; }
    string UpdatedBy { get; set; }
    DateTime UpdatedTime { get; set; }

    /// <summary>产品类别（GasDG/GasOG/WaterDW/WaterOW），用于分表路由，不映射数据库物理列</summary>
    string Category { get; set; }
}

/// <summary>整机配置实体通用接口（4 个产品 WholeConfig 实体共用）</summary>
public interface IWholeConfig : IBoardConfig
{
    string CustomerCode { get; set; }
    int PlanType { get; set; }
}

/// <summary>板卡配置审计日志通用接口</summary>
public interface IBoardConfigAuditLog
{
    long Id { get; set; }
    long RecordId { get; set; }
    string OperationType { get; set; }
    string? InsertedData { get; set; }
    string? DeletedData { get; set; }
    string CreatedBy { get; set; }
    DateTime CreatedTime { get; set; }

    /// <summary>产品类别（GasDG/GasOG/WaterDW/WaterOW），用于分表路由，不映射数据库物理列</summary>
    string Category { get; set; }
}

/// <summary>整机配置审计日志通用接口</summary>
public interface IWholeConfigAuditLog : IBoardConfigAuditLog { }

/// <summary>板卡检测记录实体通用接口（4 个产品 Boards 实体共用）</summary>
public interface IBoards
{
    long Id { get; set; }
    string OrderNumber { get; set; }
    string ProductSn { get; set; }
    string ProductCode { get; set; }
    string MaterialNumber { get; set; }
    string TestContentJson { get; set; }
    bool TestResult { get; set; }
    string CreatedBy { get; set; }
    DateTime CreatedTime { get; set; }

    /// <summary>产品类别（GasDG/GasOG/WaterDW/WaterOW），用于分表路由，不映射数据库物理列</summary>
    string Category { get; set; }
}

/// <summary>整机检测记录实体通用接口（4 个产品 Wholes 实体共用）</summary>
public interface IWholes : IBoards
{
    int TestType { get; set; }
    string CustomerCode { get; set; }
}
