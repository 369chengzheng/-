using System;
using System.Collections.Generic;

namespace IMDP.Products.GasDG.LaserDetector.Protocol
{
    /// <summary>
    /// 激光检测仪响应帧滑动拼包器，负责处理串口半包、粘包及前导噪声
    /// </summary>
    public sealed class LaserDetectorResponseFrameAssembler
    {
        private readonly List<byte> _receiveBuffer = new List<byte>();
        private readonly object _bufferLock = new object();

        #region 接收数据拼包

        /// <summary>
        /// 追加一次串口接收数据，并提取当前缓存中的全部完整响应帧
        /// </summary>
        /// <param name="receivedData">本次串口回调收到的字节数据</param>
        /// <returns>按接收顺序返回全部完整且校验正确的 0xAB 或 0xAD 响应帧</returns>
        /// <exception cref="ArgumentNullException">接收数据为空引用时抛出</exception>
        public IReadOnlyList<byte[]> AppendData(byte[] receivedData)
        {
            ArgumentNullException.ThrowIfNull(receivedData);
            if (receivedData.Length == 0)
            {
                return Array.Empty<byte[]>();
            }

            lock (_bufferLock)
            {
                _receiveBuffer.AddRange(receivedData);
                var fullResponses = new List<byte[]>();

                // 每次提取一帧后继续扫描剩余缓存，从而支持一次回调携带多帧粘包。
                while (LaserDetectorProtocol.TryParseGetFullResponse(
                    _receiveBuffer.ToArray(),
                    out byte[] fullResponse))
                {
                    int frameStartIndex = FindSequence(_receiveBuffer, fullResponse);
                    if (frameStartIndex < 0)
                    {
                        break;
                    }

                    _receiveBuffer.RemoveRange(0, frameStartIndex + fullResponse.Length);
                    fullResponses.Add(fullResponse);
                }

                TrimInvalidPrefix();
                return fullResponses;
            }
        }

        /// <summary>
        /// 清空尚未组成完整响应帧的接收缓存
        /// </summary>
        public void Clear()
        {
            lock (_bufferLock)
            {
                _receiveBuffer.Clear();
            }
        }

        #endregion

        #region 拼包辅助方法

        /// <summary>
        /// 在接收缓存中查找指定完整帧的起始位置
        /// </summary>
        /// <param name="source">当前接收缓存</param>
        /// <param name="target">已通过校验的完整响应帧</param>
        /// <returns>完整帧在缓存中的起始索引，未找到时返回 -1</returns>
        private static int FindSequence(IReadOnlyList<byte> source, IReadOnlyList<byte> target)
        {
            for (int startIndex = 0; startIndex <= source.Count - target.Count; startIndex++)
            {
                bool isMatched = true;
                for (int offset = 0; offset < target.Count; offset++)
                {
                    if (source[startIndex + offset] == target[offset])
                    {
                        continue;
                    }

                    isMatched = false;
                    break;
                }

                if (isMatched)
                {
                    return startIndex;
                }
            }

            return -1;
        }

        /// <summary>
        /// 丢弃不可能组成协议帧的缓存前缀，并保留可能尚未接收完整的帧起始数据
        /// </summary>
        private void TrimInvalidPrefix()
        {
            int potentialStartIndex = FindPotentialFrameStart();
            if (potentialStartIndex < 0)
            {
                _receiveBuffer.Clear();
                return;
            }

            if (potentialStartIndex > 0)
            {
                _receiveBuffer.RemoveRange(0, potentialStartIndex);
            }
        }

        /// <summary>
        /// 查找缓存中可能因半包而尚未完成的响应帧起始位置
        /// </summary>
        /// <returns>候选帧头索引；不存在可保留候选数据时返回 -1</returns>
        private int FindPotentialFrameStart()
        {
            for (int startIndex = 0; startIndex < _receiveBuffer.Count; startIndex++)
            {
                if (_receiveBuffer[startIndex] != LaserDetectorProtocolConstants.FrameHeader)
                {
                    continue;
                }

                int remainingLength = _receiveBuffer.Count - startIndex;
                if (remainingLength == 1)
                {
                    return startIndex;
                }

                if (!LaserDetectorProtocolConstants.IsSupportedResponseFlag(_receiveBuffer[startIndex + 1]))
                {
                    continue;
                }

                if (remainingLength == 2)
                {
                    return startIndex;
                }

                int expectedLength = _receiveBuffer[startIndex + 2];
                if (expectedLength >= LaserDetectorProtocolConstants.ShortFrameLength &&
                    remainingLength < expectedLength)
                {
                    return startIndex;
                }
            }

            return -1;
        }

        #endregion
    }
}
