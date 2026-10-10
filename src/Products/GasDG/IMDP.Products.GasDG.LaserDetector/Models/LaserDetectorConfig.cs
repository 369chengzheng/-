// =============================================================================
// 文件名：LaserDetectorConfig.cs
// 功能描述：家用可燃气体激光检测仪配置模型（数据驱动版）。
//           以统一的配置项列表替代散落的 60+ 属性，每项含名称、系统码、
//           MES 代码、启用状态、取值方式（范围/具体值/状态/测试方式）。
// =============================================================================
using System.Collections.Generic;
using System.Linq;

namespace IMDP.Products.GasDG.LaserDetector.Models
{
    /// <summary>
    /// 家用可燃气体激光检测仪测试配置
    /// <para>承载构建开始测试指令所需的全部配置项，由上层按测试方案解析后填充。</para>
    /// </summary>
    public class LaserDetectorConfig
    {
        /// <summary>
        /// 配置项列表（每项对应协议中的一个检测项目）
        /// </summary>
        public List<LaserDetectorConfigItem> Items { get; set; } = new();

        /// <summary>
        /// 按系统码获取配置项
        /// </summary>
        /// <param name="code">配置项系统码（如 VOLTAGE_12V）</param>
        /// <returns>匹配的配置项；未找到时返回 null</returns>
        public LaserDetectorConfigItem? GetItem(string code)
        {
            return Items.FirstOrDefault(item => item.Code == code);
        }

        /// <summary>
        /// 判断指定系统码的配置项是否已启用
        /// </summary>
        /// <param name="code">配置项系统码</param>
        /// <returns>项存在且已启用时返回 true；不存在或未启用时返回 false</returns>
        public bool IsEnabled(string code)
        {
            return GetItem(code) is { IsEnabled: true };
        }
    }

    /// <summary>
    /// 激光检测仪单项配置
    /// <para>以 Kind 决定取值方式：Range 用上下限，Value 用具体值，
    /// Status 仅用启用状态，TestMode 用下拉方式值（01/02）。</para>
    /// </summary>
    public class LaserDetectorConfigItem
    {
        /// <summary>检测项显示名称（如 12V电压）</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>系统识别码（协议层与判定逻辑的取值键，如 VOLTAGE_12V）</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>MES 检测项目代码（界面可编辑，如 C-12VDYJC）</summary>
        public string MesCode { get; set; } = string.Empty;

        /// <summary>是否启用该检测项</summary>
        public bool IsEnabled { get; set; }

        /// <summary>取值方式</summary>
        public LaserDetectorItemKind Kind { get; set; }

        /// <summary>范围下限（Kind=Range 时有效）</summary>
        public string? LowerLimit { get; set; }

        /// <summary>范围上限（Kind=Range 时有效）</summary>
        public string? UpperLimit { get; set; }

        /// <summary>期望具体值（Kind=Value 时有效，如版本号 1.0.1）</summary>
        public string? Value { get; set; }

        /// <summary>测试方式值（Kind=TestMode 时有效：01 或 02）</summary>
        public string? TestMode { get; set; }
    }

    /// <summary>
    /// 检测项取值方式枚举
    /// </summary>
    public enum LaserDetectorItemKind
    {
        /// <summary>范围检测：下限～上限闭区间</summary>
        Range,

        /// <summary>具体值检测：期望值完全匹配</summary>
        Value,

        /// <summary>状态检测：仅启用/禁用，协议返回正常/异常</summary>
        Status,

        /// <summary>测试方式：01/02 两种协议方式下拉</summary>
        TestMode
    }

    /// <summary>
    /// 测试方式下拉选项
    /// </summary>
    public class ConfigModeOption
    {
        /// <summary>界面显示文本</summary>
        public string Display { get; set; } = string.Empty;

        /// <summary>配置存储值（01/02）</summary>
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// 激光检测仪配置项系统码常量
    /// <para>与 <see cref="LaserDetectorConfigItem.Code"/> 对应，供协议层与判定逻辑引用。</para>
    /// </summary>
    public static class LaserDetectorItemCodes
    {
        /// <summary>12V 电压范围检测</summary>
        public const string Voltage12V = "VOLTAGE_12V";
        /// <summary>8V 电压范围检测</summary>
        public const string Voltage8V = "VOLTAGE_8V";
        /// <summary>主电 5V 电压范围检测</summary>
        public const string MainVoltage5V = "VOLTAGE_5V_MAIN";
        /// <summary>甲烷 5V 电压范围检测</summary>
        public const string MethaneVoltage5V = "VOLTAGE_5V_METHANE";
        /// <summary>3.8V 电压范围检测</summary>
        public const string Voltage3V8 = "VOLTAGE_3V8";
        /// <summary>3.3V 电压范围检测</summary>
        public const string Voltage3V3 = "VOLTAGE_3V3";
        /// <summary>功耗范围检测</summary>
        public const string PowerConsumption = "POWER";
        /// <summary>信号强度范围检测</summary>
        public const string SignalStrength = "SIGNAL";
        /// <summary>IMEI 标识号检测</summary>
        public const string Imei = "IMEI";
        /// <summary>IMSI 标识号检测</summary>
        public const string Imsi = "IMSI";
        /// <summary>ICCID 标识号检测</summary>
        public const string Iccid = "ICCID";
        /// <summary>Flash 状态检测</summary>
        public const string Flash = "FLASH";
        /// <summary>蓝牙状态检测（与星闪互斥）</summary>
        public const string Bluetooth = "BLUETOOTH";
        /// <summary>星闪状态检测（与蓝牙互斥）</summary>
        public const string StarFlash = "STARFLASH";
        /// <summary>甲烷传感器状态检测</summary>
        public const string MethaneSensor = "METHANE_SENSOR";
        /// <summary>无源测试（方式：01 仅输出，02 仅输入）</summary>
        public const string PassiveTest = "PASSIVE_TEST";
        /// <summary>有源测试（方式：01 仅输出，02 输入+输出）</summary>
        public const string ActiveTest = "ACTIVE_TEST";
        /// <summary>程序版本号检测</summary>
        public const string ProgramVersion = "VERSION";
        /// <summary>编译时间检测</summary>
        public const string CompilationTime = "COMPILE_TIME";
    }

    /// <summary>
    /// 激光检测仪默认配置项工厂
    /// <para>提供全部 19 个检测项的默认定义，供配置界面初始化使用。</para>
    /// <para>MES 代码默认为空，由产线人员按制令单要求在界面上自行配置。</para>
    /// </summary>
    public static class LaserDetectorConfigDefaults
    {
        /// <summary>
        /// 创建全部检测项的默认配置列表（按协议字段顺序排列）
        /// </summary>
        /// <returns>19 个默认配置项（MES 代码均为空）</returns>
        public static List<LaserDetectorConfigItem> CreateDefaultItems()
        {
            return new List<LaserDetectorConfigItem>
            {
                new() { Name = "12V电压", Code = LaserDetectorItemCodes.Voltage12V, Kind = LaserDetectorItemKind.Range },
                new() { Name = "8V电压", Code = LaserDetectorItemCodes.Voltage8V, Kind = LaserDetectorItemKind.Range },
                new() { Name = "主电5V电压", Code = LaserDetectorItemCodes.MainVoltage5V, Kind = LaserDetectorItemKind.Range },
                new() { Name = "甲烷5V电压", Code = LaserDetectorItemCodes.MethaneVoltage5V, Kind = LaserDetectorItemKind.Range },
                new() { Name = "3.8V电压", Code = LaserDetectorItemCodes.Voltage3V8, Kind = LaserDetectorItemKind.Range },
                new() { Name = "3.3V电压", Code = LaserDetectorItemCodes.Voltage3V3, Kind = LaserDetectorItemKind.Range },
                new() { Name = "功耗", Code = LaserDetectorItemCodes.PowerConsumption, Kind = LaserDetectorItemKind.Range },
                new() { Name = "信号强度", Code = LaserDetectorItemCodes.SignalStrength, Kind = LaserDetectorItemKind.Range },
                new() { Name = "IMEI读取", Code = LaserDetectorItemCodes.Imei, Kind = LaserDetectorItemKind.Status },
                new() { Name = "IMSI读取", Code = LaserDetectorItemCodes.Imsi, Kind = LaserDetectorItemKind.Status },
                new() { Name = "ICCID读取", Code = LaserDetectorItemCodes.Iccid, Kind = LaserDetectorItemKind.Status },
                new() { Name = "Flash检测", Code = LaserDetectorItemCodes.Flash, Kind = LaserDetectorItemKind.Status },
                new() { Name = "蓝牙检测", Code = LaserDetectorItemCodes.Bluetooth, Kind = LaserDetectorItemKind.Status },
                new() { Name = "星闪检测", Code = LaserDetectorItemCodes.StarFlash, Kind = LaserDetectorItemKind.Status },
                new() { Name = "甲烷传感器", Code = LaserDetectorItemCodes.MethaneSensor, Kind = LaserDetectorItemKind.Status },
                new() { Name = "无源测试", Code = LaserDetectorItemCodes.PassiveTest, Kind = LaserDetectorItemKind.TestMode, TestMode = "01" },
                new() { Name = "有源测试", Code = LaserDetectorItemCodes.ActiveTest, Kind = LaserDetectorItemKind.TestMode, TestMode = "01" },
                new() { Name = "版本号", Code = LaserDetectorItemCodes.ProgramVersion, Kind = LaserDetectorItemKind.Value },
                new() { Name = "编译时间", Code = LaserDetectorItemCodes.CompilationTime, Kind = LaserDetectorItemKind.Value }
            };
        }
    }
}
