using System;
using System.Collections.Generic;

namespace IMDP.Products.GasDG.LaserDetector.Models
{
    /// <summary>
    /// 激光检测仪单工位自动测试结果
    /// <para>由 0xAD 测试结果响应帧解析生成，承载全部协议数据域字段。</para>
    /// </summary>
    public sealed class LaserDetectorTestResult
    {
        /// <summary>界面工位号（正整数，来源数据库方案配置）</summary>
        public int Station { get; set; }
        /// <summary>12V 电压，单位 V</summary>
        public double Voltage12V { get; set; }
        /// <summary>8V 电压，单位 V</summary>
        public double Voltage8V { get; set; }
        /// <summary>主电 5V 电压，单位 V</summary>
        public double MainVoltage5V { get; set; }
        /// <summary>甲烷 5V 电压，单位 V</summary>
        public double MethaneVoltage5V { get; set; }
        /// <summary>3.8V 电压，单位 V</summary>
        public double Voltage3V8 { get; set; }
        /// <summary>3.3V 电压，单位 V</summary>
        public double Voltage3V3 { get; set; }
        /// <summary>功耗值，协议原始值除以 10</summary>
        public double PowerConsumption { get; set; }
        /// <summary>设备 IMEI</summary>
        public string Imei { get; set; } = string.Empty;
        /// <summary>设备 IMSI</summary>
        public string Imsi { get; set; } = string.Empty;
        /// <summary>设备 ICCID</summary>
        public string Iccid { get; set; } = string.Empty;
        /// <summary>信号强度十进制值</summary>
        public int SignalStrength { get; set; }
        /// <summary>Flash 检测状态</summary>
        public byte FlashStatus { get; set; }
        /// <summary>蓝牙或星闪检测状态（0x01 蓝牙正常，0x02 星闪正常）</summary>
        public byte BluetoothStarFlashStatus { get; set; }
        /// <summary>星闪 MAC 地址</summary>
        public string StarFlashMacAddress { get; set; } = string.Empty;
        /// <summary>无源测试状态</summary>
        public byte PassiveTestStatus { get; set; }
        /// <summary>有源测试状态</summary>
        public byte ActiveTestStatus { get; set; }
        /// <summary>甲烷传感器状态</summary>
        public byte MethaneSensorStatus { get; set; }
        /// <summary>红外状态（协议固定返回，无配置开关）</summary>
        public byte InfraredStatus { get; set; }
        /// <summary>程序版本号</summary>
        public string ProgramVersion { get; set; } = string.Empty;
        /// <summary>程序版本号是否由有效的非零协议数据生成</summary>
        public bool IsProgramVersionValid { get; set; }
        /// <summary>程序编译时间</summary>
        public DateTime? CompilationTime { get; set; }
        /// <summary>程序编译时间的页面展示文本</summary>
        public string CompilationTimeDisplay { get; set; } = string.Empty;
        /// <summary>程序编译时间是否为有效日期</summary>
        public bool IsCompilationTimeValid { get; set; }

        #region MES 测试数据生成

        /// <summary>
        /// 根据当前配置生成可上传 MES 的自动检测项目列表
        /// <para>遍历启用配置项，按 <see cref="LaserDetectorConfigItem.MesCode"/> 生成
        /// {Code=MesCode, Data=实测值, Result=判定"1"/"0"}；实测值与红绿判定同
        /// <see cref="CreateDisplayItem"/> 逻辑（界面展示与 MES 上传同一数据源）。
        /// MesCode 为空的项跳过（产线未在配置界面填写编码，无法被 MES 测试项匹配）。</para>
        /// </summary>
        /// <param name="config">当前产品的激光检测配置</param>
        /// <returns>MES 测试数据列表（仅含启用且 MesCode 非空的项）</returns>
        public IReadOnlyList<Domain.Interfaces.MesProductItem> CreateMesItems(LaserDetectorConfig config)
        {
            var items = new List<Domain.Interfaces.MesProductItem>();
            foreach (var item in config.Items)
            {
                if (!item.IsEnabled || string.IsNullOrWhiteSpace(item.MesCode))
                {
                    continue;
                }

                var displayItem = CreateDisplayItem(item);
                if (displayItem == null)
                {
                    continue;
                }

                // 蓝牙/星闪项的 MES 数据按产线要求上传 0/1（正常"1"、异常"0"），
                // 不再上传"星闪/蓝牙"等展示文本（MAC 地址仅界面展示，不上传）；
                // 其余检测项仍上传实测值文本
                string mesData = item.Code is LaserDetectorItemCodes.Bluetooth or LaserDetectorItemCodes.StarFlash
                    ? (displayItem.IsPassed ? "1" : "0")
                    : displayItem.Value;
                items.Add(new Domain.Interfaces.MesProductItem
                {
                    Code = item.MesCode,
                    Data = mesData,
                    Result = displayItem.IsPassed ? "1" : "0"
                });
            }

            return items;
        }

        /// <summary>
        /// 生成全部自动检测项均为 0 的 MES 占位数据（AC 轮询总超时场景）
        /// <para>遍历启用配置项，Data 与 Result 全部置 "0"（MesCode 为空的项跳过），
        /// 满足「查询超时无响应则所有测试值全为 0、测试结果为 0」的上传要求。</para>
        /// </summary>
        /// <param name="config">当前产品的激光检测配置</param>
        /// <returns>全 0 MES 测试数据列表</returns>
        public static IReadOnlyList<Domain.Interfaces.MesProductItem> CreateAllZeroMesItems(LaserDetectorConfig config)
        {
            var items = new List<Domain.Interfaces.MesProductItem>();
            foreach (var item in config.Items)
            {
                if (!item.IsEnabled || string.IsNullOrWhiteSpace(item.MesCode))
                {
                    continue;
                }

                items.Add(new Domain.Interfaces.MesProductItem
                {
                    Code = item.MesCode,
                    Data = "0",
                    Result = "0"
                });
            }

            return items;
        }

        #endregion

        #region 结果判断

        /// <summary>
        /// 根据当前配置判断自动测试状态项是否全部正常
        /// <para>遍历配置项列表，按取值方式（范围/具体值/状态/测试方式）分派判定。</para>
        /// </summary>
        /// <param name="config">当前产品的激光检测配置</param>
        /// <returns>所有启用项均通过时返回 true，否则返回 false</returns>
        public bool IsAutomaticTestPassed(LaserDetectorConfig config)
        {
            foreach (var item in config.Items)
            {
                if (!item.IsEnabled)
                {
                    continue;
                }

                if (!IsItemPassed(item))
                {
                    return false;
                }
            }

            // 红外为协议固定返回状态，当前未提供关闭该检测的配置项。
            return InfraredStatus == 0x01;
        }

        /// <summary>
        /// 判断单个配置项是否通过检测
        /// </summary>
        /// <param name="item">待判定的配置项</param>
        /// <returns>该配置项按其取值方式判定通过时返回 true</returns>
        private bool IsItemPassed(LaserDetectorConfigItem item)
        {
            return item.Code switch
            {
                LaserDetectorItemCodes.Voltage12V => IsValueWithinRange(Voltage12V, item),
                LaserDetectorItemCodes.Voltage8V => IsValueWithinRange(Voltage8V, item),
                LaserDetectorItemCodes.MainVoltage5V => IsValueWithinRange(MainVoltage5V, item),
                LaserDetectorItemCodes.MethaneVoltage5V => IsValueWithinRange(MethaneVoltage5V, item),
                LaserDetectorItemCodes.Voltage3V8 => IsValueWithinRange(Voltage3V8, item),
                LaserDetectorItemCodes.Voltage3V3 => IsValueWithinRange(Voltage3V3, item),
                LaserDetectorItemCodes.PowerConsumption => IsValueWithinRange(PowerConsumption, item),
                LaserDetectorItemCodes.SignalStrength => IsValueWithinRange(SignalStrength, item),
                LaserDetectorItemCodes.Imei => IsIdentifierValid(Imei),
                LaserDetectorItemCodes.Imsi => IsIdentifierValid(Imsi),
                LaserDetectorItemCodes.Iccid => IsIdentifierValid(Iccid),
                LaserDetectorItemCodes.Flash => FlashStatus == 0x01,
                LaserDetectorItemCodes.Bluetooth => BluetoothStarFlashStatus == 0x01,
                LaserDetectorItemCodes.StarFlash => BluetoothStarFlashStatus == 0x02 && IsIdentifierValid(StarFlashMacAddress),
                LaserDetectorItemCodes.MethaneSensor => MethaneSensorStatus == 0x01,
                LaserDetectorItemCodes.PassiveTest => IsTestModeEnabled(item) && PassiveTestStatus == 0x01,
                LaserDetectorItemCodes.ActiveTest => IsTestModeEnabled(item) && ActiveTestStatus == 0x01,
                LaserDetectorItemCodes.ProgramVersion => IsProgramVersionMatched(item),
                LaserDetectorItemCodes.CompilationTime => IsCompilationTimeMatched(item),
                _ => true
            };
        }

        /// <summary>
        /// 判断数值结果是否位于配置项的闭区间内
        /// </summary>
        /// <param name="value">协议解析后的实际数值</param>
        /// <param name="item">范围型配置项</param>
        /// <returns>配置范围有效且数值位于范围内时返回 true</returns>
        private static bool IsValueWithinRange(double value, LaserDetectorConfigItem item)
        {
            return double.TryParse(item.LowerLimit, out double minimum) &&
                double.TryParse(item.UpperLimit, out double maximum) &&
                minimum <= maximum &&
                value >= minimum &&
                value <= maximum;
        }

        /// <summary>
        /// 判断程序版本是否满足已配置的期望版本
        /// </summary>
        /// <param name="item">版本号配置项（Value 为期望版本）</param>
        /// <returns>未配置版本时返回 true；已配置时版本有效且完全一致才返回 true</returns>
        private bool IsProgramVersionMatched(LaserDetectorConfigItem item)
        {
            return string.IsNullOrWhiteSpace(item.Value) ||
                (IsProgramVersionValid && ProgramVersion == item.Value);
        }

        /// <summary>
        /// 判断编译时间是否满足已配置的期望时间
        /// </summary>
        /// <param name="item">编译时间配置项（Value 为 yyMMddHHmmss 格式期望值）</param>
        /// <returns>未配置时返回 true；已配置时解析有效且格式化值完全一致才返回 true</returns>
        private bool IsCompilationTimeMatched(LaserDetectorConfigItem item)
        {
            return string.IsNullOrWhiteSpace(item.Value) ||
                (IsCompilationTimeValid &&
                    CompilationTime.HasValue &&
                    CompilationTime.Value.ToString("yyMMddHHmmss") == item.Value);
        }

        /// <summary>
        /// 判断 IMEI、IMSI 或 ICCID 是否为有效检测结果
        /// </summary>
        /// <param name="value">协议解析出的标识号文本</param>
        /// <returns>非空、非全零、不包含换行且非全 FF 时返回 true</returns>
        private static bool IsIdentifierValid(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Contains('\r') ||
                value.Contains('\n'))
            {
                return false;
            }

            bool isAllZero = true;
            bool isAllFF = true;
            foreach (char character in value)
            {
                if (character != '0')
                {
                    isAllZero = false;
                }

                if (character != 'F' && character != 'f')
                {
                    isAllFF = false;
                }
            }

            return !isAllZero && !isAllFF;
        }

        /// <summary>
        /// 判断测试方式型配置项是否选择了实际测试方式
        /// </summary>
        /// <param name="item">测试方式型配置项</param>
        /// <returns>测试方式为非 00 值时返回 true</returns>
        private static bool IsTestModeEnabled(LaserDetectorConfigItem item)
        {
            return item.TestMode != "00";
        }

        /// <summary>
        /// 根据当前配置生成页面需要展示的全部自动测试结果项
        /// <para>按协议字段顺序排列，每项含名称、展示值与红绿判定；
        /// 未启用的配置项不出现在结果列表中。</para>
        /// </summary>
        /// <param name="config">当前产品的激光检测配置</param>
        /// <returns>按协议字段顺序排列的结果展示项列表</returns>
        public IReadOnlyList<LaserDetectorResultDisplayItem> CreatePropertyItems(LaserDetectorConfig config)
        {
            var items = new List<LaserDetectorResultDisplayItem>();
            foreach (var item in config.Items)
            {
                if (!item.IsEnabled)
                {
                    continue;
                }

                var displayItem = CreateDisplayItem(item);
                if (displayItem != null)
                {
                    items.Add(displayItem);
                }
            }

            return items;
        }

        /// <summary>
        /// 为单个启用配置项生成结果展示项（含红绿判定）
        /// </summary>
        /// <param name="item">已启用的配置项</param>
        /// <returns>展示项；该项无对应协议数据时返回 null</returns>
        private LaserDetectorResultDisplayItem? CreateDisplayItem(LaserDetectorConfigItem item)
        {
            string value = item.Code switch
            {
                LaserDetectorItemCodes.Voltage12V => $"{Voltage12V:0.###} V",
                LaserDetectorItemCodes.Voltage8V => $"{Voltage8V:0.###} V",
                LaserDetectorItemCodes.MainVoltage5V => $"{MainVoltage5V:0.###} V",
                LaserDetectorItemCodes.MethaneVoltage5V => $"{MethaneVoltage5V:0.###} V",
                LaserDetectorItemCodes.Voltage3V8 => $"{Voltage3V8:0.###} V",
                LaserDetectorItemCodes.Voltage3V3 => $"{Voltage3V3:0.###} V",
                LaserDetectorItemCodes.PowerConsumption => PowerConsumption.ToString("0.0"),
                LaserDetectorItemCodes.Imei => Imei,
                LaserDetectorItemCodes.Imsi => Imsi,
                LaserDetectorItemCodes.Iccid => Iccid,
                LaserDetectorItemCodes.SignalStrength => SignalStrength.ToString(),
                LaserDetectorItemCodes.Flash => GetNormalStatus(FlashStatus),
                LaserDetectorItemCodes.Bluetooth => GetWirelessStatus(),
                LaserDetectorItemCodes.StarFlash => GetStarFlashDisplay(),
                LaserDetectorItemCodes.MethaneSensor => GetNormalStatus(MethaneSensorStatus),
                LaserDetectorItemCodes.PassiveTest => GetNormalStatus(PassiveTestStatus),
                LaserDetectorItemCodes.ActiveTest => GetNormalStatus(ActiveTestStatus),
                LaserDetectorItemCodes.ProgramVersion => ProgramVersion,
                LaserDetectorItemCodes.CompilationTime => CompilationTimeDisplay,
                _ => string.Empty
            };

            if (value == string.Empty)
            {
                return null;
            }

            // 按该项配置判定红绿：通过为绿，未通过为红
            bool isPassed = IsItemPassed(item);
            return new LaserDetectorResultDisplayItem(item.Name, value, isPassed);
        }

        /// <summary>
        /// 将状态检测值转换为展示文本
        /// </summary>
        /// <param name="value">协议状态字节</param>
        /// <returns>0x01 时返回"正常"，否则返回"异常"</returns>
        private static string GetNormalStatus(byte value)
        {
            return value == 0x01 ? "正常" : "异常";
        }

        /// <summary>
        /// 将蓝牙/星闪检测值转换为展示文本
        /// </summary>
        /// <returns>异常/蓝牙/星闪/未知状态文本</returns>
        private string GetWirelessStatus()
        {
            return BluetoothStarFlashStatus switch
            {
                0x00 => "异常",
                0x01 => "蓝牙",
                0x02 => "星闪",
                _ => $"未知({BluetoothStarFlashStatus})"
            };
        }

        /// <summary>
        /// 生成星闪项的界面展示文本：状态 + 星闪 MAC 地址
        /// <para>产线要求星闪时将读到的 MAC 地址一并在界面显示；
        /// MAC 已由协议层按 ASCII 解析（定长 12 字节，去除尾部填充），
        /// 此处直接拼接展示。状态异常（非 0x02）时 MAC 字段无意义，仅展示状态文本。</para>
        /// </summary>
        /// <returns>形如"星闪 582D341A2B3C"的展示文本；非星闪状态时仅返回状态文本</returns>
        private string GetStarFlashDisplay()
        {
            string status = GetWirelessStatus();
            if (BluetoothStarFlashStatus == 0x02 && !string.IsNullOrWhiteSpace(StarFlashMacAddress))
            {
                return $"{status} {StarFlashMacAddress}";
            }

            return status;
        }

        #endregion
    }

    /// <summary>
    /// 自动测试结果展示项
    /// <para>承载单个检测项在工位"自动测试结果"列表中的名称、实测值与红绿判定。</para>
    /// </summary>
    public sealed class LaserDetectorResultDisplayItem
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">检测项名称</param>
        /// <param name="value">实测值展示文本</param>
        /// <param name="isPassed">该项是否判定通过</param>
        public LaserDetectorResultDisplayItem(string name, string value, bool isPassed)
        {
            Name = name;
            Value = value;
            IsPassed = isPassed;
        }

        /// <summary>检测项名称</summary>
        public string Name { get; }

        /// <summary>实测值展示文本</summary>
        public string Value { get; }

        /// <summary>该项是否判定通过（true=绿色，false=红色）</summary>
        public bool IsPassed { get; }
    }
}
