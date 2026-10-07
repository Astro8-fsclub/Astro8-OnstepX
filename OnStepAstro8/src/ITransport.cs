using System;

namespace OnStepAstro8
{
    /// <summary>
    /// OnStep 物理传输层抽象：HID 转串口芯片（USB）与 TCP（WiFi）共用同一协议栈。
    /// 上层 OnStepProtocol 只依赖本接口，收发均为"原始串口字节流 + '#' 应答结束符"语义；
    /// HID 实现内部负责 32 字节报表的组帧/解帧。
    /// </summary>
    public interface ITransport : IDisposable
    {
        /// <summary>是否已打开（已连接）。</summary>
        bool IsOpen { get; }

        /// <summary>连接描述（用于显示/报错，如 "HID 1A86:55D4"、"TCP 192.168.0.1:9998"）。</summary>
        string Description { get; }

        /// <summary>打开/建立连接；失败抛异常。</summary>
        void Open();

        /// <summary>打开后立即调用，排空缓冲区内残留数据（避免干扰握手）。</summary>
        void FlushPending();

        /// <summary>发送原始字节（不含 OnStep 帧格式，纯数据）。</summary>
        void Write(byte[] data);

        /// <summary>
        /// 读取数据直到遇到 '#'（OnStep/LX200 应答结束符）或超时。
        /// 返回的字符串包含结尾 '#'；超时且无任何数据时抛 TimeoutException。
        /// </summary>
        string ReadUntilHash(int timeoutMs);

        /// <summary>关闭连接。</summary>
        void Close();
    }
}
