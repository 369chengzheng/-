using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using IMDP.Products.GasDG.LaserDetector.Models;

namespace IMDP.Products.GasDG.LaserDetector.Protocol
{
    /// <summary>
    /// 家用可燃气体激光检测仪通信协议指令生成器
    /// <para>负责构建开始测试、查询测试结果指令，并解析工装返回的开始测试响应与测试结果响应。</para>
    /// </summary>
    public static class LaserDetectorProtocol
    {
        #region 开始测试指令

        /// <summary>
        /// 创建指定工位的开始测试指令
        /// </summary>
        /// <param name="station">界面工位号（正整数，来源数据库方案配置）</param>
        /// <param name="config">当前产品的激光检测配置</param>
        /// <returns>完整的开始测试指令字节数组</returns>
        /// <exception cref="ArgumentOutOfRangeException">工位号或测试方式超出协议范围时抛出</exception>
        /// <exception cref="ArgumentNullException">配置对象为空时抛出</exception>
        public static byte[] CreateStartTestCommand(int station, LaserDetectorConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(config.Items);

            byte activeTest = GetTestValue(config, LaserDetectorItemCodes.ActiveTest, "有源测试");
            byte passiveTest = GetTestValue(config, LaserDetectorItemCodes.PassiveTest, "无源测试");
            // 功耗配置勾选即表示需要测试，协议固定使用 01 表示测试。
            byte powerTest = config.IsEnabled(LaserDetectorItemCodes.PowerConsumption) ? (byte)0x01 : (byte)0x00;

            byte protocolStation = GetProtocolStation(station);
            var frame = new List<byte>(9)
            {
                LaserDetectorProtocolConstants.FrameHeader,
                LaserDetectorProtocolConstants.StartTestFlag,
                0x00,
                protocolStation,
                activeTest,
                passiveTest,
                powerTest
            };

            return CompleteCommandFrame(frame);
        }

        #endregion

        #region 查询测试结果指令

        /// <summary>
        /// 创建指定工位的查询测试结果指令
        /// </summary>
        /// <param name="station">界面工位号（正整数，来源数据库方案配置）</param>
        /// <returns>完整的查询测试结果指令字节数组</returns>
        /// <exception cref="ArgumentOutOfRangeException">工位号超出协议范围时抛出</exception>
        public static byte[] CreateQueryTestResultCommand(int station)
        {
            byte protocolStation = GetProtocolStation(station);
            var frame = new List<byte>(LaserDetectorProtocolConstants.ShortFrameLength)
            {
                LaserDetectorProtocolConstants.FrameHeader,
                LaserDetectorProtocolConstants.QueryTestResultFlag,
                0x00,
                protocolStation
            };

            return CompleteCommandFrame(frame);
        }

        #endregion

        #region 通用响应帧解析

        /// <summary>
        /// 使用滑动窗口从接收数据中查找并校验一帧完整的工装响应
        /// </summary>
        /// <param name="response">串口当前已接收的数据，可包含前导噪声、半包或多帧粘包</param>
        /// <param name="fullResponse">解析成功时返回首个完整且校验正确的 0xAB 或 0xAD 响应帧</param>
        /// <returns>找到完整有效响应帧时返回 true，否则返回 false</returns>
        public static bool TryParseGetFullResponse(byte[] response, out byte[] fullResponse)
        {
            fullResponse = Array.Empty<byte>();
            if (response == null || response.Length < LaserDetectorProtocolConstants.ShortFrameLength)
            {
                return false;
            }

            // 从每一个可能的帧头开始滑动，损坏帧不会阻塞后续有效帧。
            for (int startIndex = 0; startIndex <= response.Length - LaserDetectorProtocolConstants.ShortFrameLength; startIndex++)
            {
                if (!TryValidateResponseAt(response, startIndex, out int frameLength))
                {
                    continue;
                }

                fullResponse = new byte[frameLength];
                Array.Copy(response, startIndex, fullResponse, 0, frameLength);
                return true;
            }

            return false;
        }

        #endregion

        #region 测试结果响应解析

        /// <summary>
        /// 解析并校验工装返回的 0xAD 测试结果响应
        /// </summary>
        /// <param name="response">完整的 0xAD 响应帧</param>
        /// <param name="config">当前产品配置，用于决定功耗及星闪 MAC 的读取方式</param>
        /// <param name="testResult">解析成功时返回单工位自动测试结果</param>
        /// <returns>帧校验和数据域格式全部正确时返回 true，否则返回 false</returns>
        public static bool TryParseTestResultResponse(
            byte[] response,
            LaserDetectorConfig config,
            out LaserDetectorTestResult testResult)
        {
            testResult = new LaserDetectorTestResult();
            ArgumentNullException.ThrowIfNull(config);
            if (!TryParseGetFullResponse(response, out byte[] fullResponse) ||
                fullResponse[1] != LaserDetectorProtocolConstants.TestResultResponseFlag ||
                fullResponse.Length - LaserDetectorProtocolConstants.ShortFrameLength < LaserDetectorProtocolConstants.TestResultDataLength)
            {
                return false;
            }

            const int dataOffset = LaserDetectorProtocolConstants.TestResultDataOffset;
            DateTime? compilationTime = ParseCompilationTime(
                fullResponse,
                dataOffset + 85,
                out string compilationTimeDisplay,
                out bool isCompilationTimeValid);
            bool isProgramVersionValid = !IsAllZero(fullResponse, dataOffset + 83, 2);
            bool shouldParsePowerConsumption = config.IsEnabled(LaserDetectorItemCodes.PowerConsumption);
            bool shouldReadStarFlashMac = config.IsEnabled(LaserDetectorItemCodes.StarFlash);

            testResult = new LaserDetectorTestResult
            {
                Station = fullResponse[3] + 1,
                Voltage12V = ReadUInt16LittleEndian(fullResponse, dataOffset) / 1000.0,
                Voltage8V = ReadUInt16LittleEndian(fullResponse, dataOffset + 2) / 1000.0,
                MainVoltage5V = ReadUInt16LittleEndian(fullResponse, dataOffset + 4) / 1000.0,
                MethaneVoltage5V = ReadUInt16LittleEndian(fullResponse, dataOffset + 6) / 1000.0,
                Voltage3V8 = ReadUInt16LittleEndian(fullResponse, dataOffset + 8) / 1000.0,
                Voltage3V3 = ReadUInt16LittleEndian(fullResponse, dataOffset + 10) / 1000.0,
                // 未启用功耗测试时仅跳过固定数据位，不写入业务结果对象。
                PowerConsumption = shouldParsePowerConsumption
                    ? ReadUInt16LittleEndian(fullResponse, dataOffset + 12) / 10.0
                    : 0.0,
                Imei = ReadIdentifier(fullResponse, dataOffset + 14, 15),
                Imsi = ReadIdentifier(fullResponse, dataOffset + 29, 15),
                Iccid = ReadIdentifier(fullResponse, dataOffset + 44, 20),
                SignalStrength = fullResponse[dataOffset + 64],
                FlashStatus = fullResponse[dataOffset + 65],
                BluetoothStarFlashStatus = fullResponse[dataOffset + 66],
                // 蓝牙模式下该区域不属于有效业务数据，严格按配置跳过读取。
                StarFlashMacAddress = shouldReadStarFlashMac
                    ? ReadIdentifier(fullResponse, dataOffset + 67, 12)
                    : string.Empty,
                PassiveTestStatus = fullResponse[dataOffset + 79],
                ActiveTestStatus = fullResponse[dataOffset + 80],
                MethaneSensorStatus = fullResponse[dataOffset + 81],
                InfraredStatus = fullResponse[dataOffset + 82],
                ProgramVersion = ParseProgramVersion(
                    fullResponse[dataOffset + 83],
                    fullResponse[dataOffset + 84]),
                IsProgramVersionValid = isProgramVersionValid,
                CompilationTime = compilationTime,
                CompilationTimeDisplay = compilationTimeDisplay,
                IsCompilationTimeValid = isCompilationTimeValid
            };

            return true;
        }

        #endregion

        #region 协议辅助方法

        /// <summary>
        /// 从协议数据中读取无符号 16 位小端整数
        /// </summary>
        /// <param name="data">协议数据</param>
        /// <param name="offset">低字节所在索引</param>
        /// <returns>小端序转换后的无符号整数</returns>
        private static ushort ReadUInt16LittleEndian(byte[] data, int offset)
        {
            return (ushort)(data[offset] | data[offset + 1] << 8);
        }

        /// <summary>
        /// 按协议原始顺序读取固定长度 ASCII 文本
        /// </summary>
        /// <param name="data">协议数据</param>
        /// <param name="offset">文本起始索引</param>
        /// <param name="length">文本固定长度</param>
        /// <returns>移除尾部空字符、空格和 0xFF 后的文本</returns>
        private static string ReadAscii(byte[] data, int offset, int length)
        {
            int actualLength = length;
            while (actualLength > 0)
            {
                byte tail = data[offset + actualLength - 1];
                if (tail != 0x00 && tail != 0x20 && tail != 0xFF)
                {
                    break;
                }

                actualLength--;
            }

            return Encoding.ASCII.GetString(data, offset, actualLength);
        }

        /// <summary>
        /// 读取固定长度标识号，并将全 0xFF 的异常数据保留为可见十六进制文本
        /// </summary>
        /// <param name="data">协议原始数据</param>
        /// <param name="offset">标识号起始索引</param>
        /// <param name="length">标识号固定长度</param>
        /// <returns>ASCII 标识号，或全 0xFF 时对应的十六进制文本</returns>
        private static string ReadIdentifier(byte[] data, int offset, int length)
        {
            bool isAllFF = true;
            for (int index = offset; index < offset + length; index++)
            {
                if (data[index] != 0xFF)
                {
                    isAllFF = false;
                    break;
                }
            }

            if (!isAllFF)
            {
                return ReadAscii(data, offset, length);
            }

            var valueBuilder = new StringBuilder(length * 2);
            for (int index = 0; index < length; index++)
            {
                valueBuilder.Append("FF");
            }

            return valueBuilder.ToString();
        }

        /// <summary>
        /// 解析两字节程序版本号
        /// </summary>
        /// <param name="majorVersion">主版本号</param>
        /// <param name="minorPatchVersion">高半字节为次版本号，低半字节为修订号</param>
        /// <returns>点分十进制版本号</returns>
        private static string ParseProgramVersion(byte majorVersion, byte minorPatchVersion)
        {
            int minorVersion = minorPatchVersion >> 4;
            int patchVersion = minorPatchVersion & 0x0F;
            return $"{majorVersion}.{minorVersion}.{patchVersion}";
        }

        /// <summary>
        /// 按秒、分、时、日、月、年顺序解析六字节编译时间。
        /// 无效或全零日期不阻断结果帧解析，而是以异常状态返回给页面展示。
        /// </summary>
        /// <param name="data">协议数据</param>
        /// <param name="offset">编译时间起始索引</param>
        /// <param name="compilationTimeDisplay">适用于页面展示的编译时间文本</param>
        /// <param name="isValid">编译时间字段是否为有效日期</param>
        /// <returns>日期有效时返回对应时间；全零或不合法时返回 null</returns>
        private static DateTime? ParseCompilationTime(
            byte[] data,
            int offset,
            out string compilationTimeDisplay,
            out bool isValid)
        {
            if (IsAllZero(data, offset, 6))
            {
                compilationTimeDisplay = "000000000000";
                isValid = false;
                return null;
            }

            try
            {
                DateTime compilationTime = new DateTime(
                    2000 + data[offset + 5],
                    data[offset + 4],
                    data[offset + 3],
                    data[offset + 2],
                    data[offset + 1],
                    data[offset]);
                compilationTimeDisplay = compilationTime.ToString("yyMMddHHmmss", CultureInfo.InvariantCulture);
                isValid = true;
                return compilationTime;
            }
            catch (ArgumentOutOfRangeException)
            {
                compilationTimeDisplay = "无效编译时间";
                isValid = false;
                return null;
            }
        }

        /// <summary>
        /// 判断指定协议数据片段是否全部为零。
        /// </summary>
        /// <param name="data">协议数据</param>
        /// <param name="offset">待检查片段的起始索引</param>
        /// <param name="length">待检查的字节数</param>
        /// <returns>片段中的所有字节均为 0x00 时返回 true</returns>
        private static bool IsAllZero(byte[] data, int offset, int length)
        {
            for (int index = offset; index < offset + length; index++)
            {
                if (data[index] != 0x00)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 校验接收数据中指定位置开始的响应帧
        /// </summary>
        /// <param name="response">待校验的接收数据</param>
        /// <param name="startIndex">候选帧头在接收数据中的索引</param>
        /// <param name="frameLength">校验成功时返回协议声明的整包长度</param>
        /// <returns>帧头、指示位、长度、工位、和校验及帧尾均正确时返回 true</returns>
        private static bool TryValidateResponseAt(byte[] response, int startIndex, out int frameLength)
        {
            frameLength = 0;
            if (response[startIndex] != LaserDetectorProtocolConstants.FrameHeader ||
                !LaserDetectorProtocolConstants.IsSupportedResponseFlag(response[startIndex + 1]))
            {
                return false;
            }

            frameLength = response[startIndex + 2];
            if (frameLength < LaserDetectorProtocolConstants.ShortFrameLength ||
                frameLength > response.Length - startIndex ||
                response[startIndex + 3] > 0x02)
            {
                return false;
            }

            int checksumIndex = startIndex + frameLength - 2;
            int tailIndex = startIndex + frameLength - 1;
            if (response[tailIndex] != LaserDetectorProtocolConstants.FrameTail)
            {
                return false;
            }

            byte checksum = 0x00;
            for (int index = startIndex; index < checksumIndex; index++)
            {
                checksum = unchecked((byte)(checksum + response[index]));
            }

            return response[checksumIndex] == checksum;
        }

        /// <summary>
        /// 将界面工位号转换为协议工位值
        /// </summary>
        /// <param name="station">界面工位号（正整数，来源数据库方案配置）</param>
        /// <returns>协议工位值（station - 1 的字节表示）</returns>
        /// <exception cref="ArgumentOutOfRangeException">工位号超出字节表示范围时抛出</exception>
        private static byte GetProtocolStation(int station)
        {
            if (station < 1 || station > 255)
            {
                throw new ArgumentOutOfRangeException(nameof(station), station, "工位号必须在 1～255 范围内。");
            }

            return (byte)(station - 1);
        }

        /// <summary>
        /// 根据配置项的启用状态与测试方式获取协议配置值
        /// </summary>
        /// <param name="config">当前产品的激光检测配置</param>
        /// <param name="code">测试项系统码（ACTIVE_TEST 或 PASSIVE_TEST）</param>
        /// <param name="itemName">测试项名称（用于异常提示）</param>
        /// <returns>未启用时返回 0x00，启用时返回测试方式值</returns>
        /// <exception cref="ArgumentOutOfRangeException">测试方式格式错误或超出协议范围时抛出</exception>
        private static byte GetTestValue(LaserDetectorConfig config, string code, string itemName)
        {
            var item = config.GetItem(code);
            if (item is not { IsEnabled: true })
            {
                return 0x00;
            }

            bool isValid = byte.TryParse(
                item.TestMode,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out byte value);
            if (!isValid || value > 0x02)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(item.TestMode),
                    item.TestMode,
                    $"{itemName}测试方式必须在 00～02 范围内。");
            }

            return value;
        }

        /// <summary>
        /// 计算和校验，累加当前帧中校验字节之前的所有字节并取低 8 位
        /// </summary>
        /// <param name="frame">尚未追加校验字节和帧尾的指令帧</param>
        /// <returns>和校验结果</returns>
        private static byte CalculateChecksum(IEnumerable<byte> frame)
        {
            byte checksum = 0x00;
            foreach (byte value in frame)
            {
                checksum = unchecked((byte)(checksum + value));
            }

            return checksum;
        }

        /// <summary>
        /// 根据已组装的数据域回填第三字节整包长度，并追加和校验和固定帧尾
        /// </summary>
        /// <param name="frame">包含帧头、指令、长度占位字节和数据域的待完成指令帧</param>
        /// <returns>第三字节与实际数组长度一致的完整协议指令帧</returns>
        /// <exception cref="ArgumentException">帧头、指令或长度占位字节不完整时抛出</exception>
        /// <exception cref="InvalidOperationException">完整帧长度超过单字节可表示范围时抛出</exception>
        private static byte[] CompleteCommandFrame(List<byte> frame)
        {
            if (frame == null || frame.Count < 3)
            {
                throw new ArgumentException("协议指令至少应包含帧头、指令和长度字节。", nameof(frame));
            }

            int totalLength = frame.Count + 2;
            if (totalLength > byte.MaxValue)
            {
                throw new InvalidOperationException("协议指令长度超过单字节长度字段可表示范围。");
            }

            frame[2] = (byte)totalLength;
            frame.Add(CalculateChecksum(frame));
            frame.Add(LaserDetectorProtocolConstants.FrameTail);
            return frame.ToArray();
        }

        #endregion
    }
}
