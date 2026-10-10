using IMDP.Domain.Products;

namespace IMDP.Application.DTOs;

/// <summary>按功能类型去重后保留的产品菜单项。</summary>
public sealed record ProductMenuItemDto(
    string ProductKey,
    FunctionModuleType ModuleType,
    string DisplayName);
