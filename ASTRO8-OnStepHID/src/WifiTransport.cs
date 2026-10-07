using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ASTRO8_OnStepHID
{
    /// <summary>
    /// OnStep WiFi（原始 TCP）传输层。
    /// OnStep 的 WiFi 模块（ESP32/ESP8266 板载或串口转 WiFi）把 TCP 端口上的数据
    /// 原样转发到望远镜串口：命令以 ':' 开头 '#' 结尾，应答以 '#' 结尾。
    /// 参考原 ASCOM 串口驱动的连接样式：地址 + 端口，默认 192.168.0.1:9998。
    /// </summary>
    [System.Runtime.InteropServices.ComVisible(false)]
    public sealed class WifiTransport : ITransport
    {
        private readonly object _sync = new object();
        private TcpClient _client;
        private NetworkStream _stream;

        /// <summary>目标地址（IP 或主机名）。</summary>
        public string Host { get; private set; }
        /// <summary>目标端口。</summary>
        public int Port { get; private set; }

        public WifiTransport(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("WiFi 地址不能为空", "host");
            Host = host.Trim();
            Port = port;
        }

        public bool IsOpen { get { lock (_sync) { return _stream != null; } } }

        public string Description { get { return string.Format("TCP {0}:{1}", Host, Port); } }

        /// <summary>建立 TCP 连接（5 秒超时）。</summary>
        public void Open()
        {
            Close();
            TcpClient c = new TcpClient();
            try
            {
                IAsyncResult ar = c.BeginConnect(Host, Port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(5000))
                    throw new TimeoutException(string.Format("连接 {0}:{1} 超时（5 秒）。请确认：①OnStep WiFi 已启动；②电脑与 OnStep 在同一局域网；③地址/端口正确。", Host, Port));
                c.EndConnect(ar);
                NetworkStream s = c.GetStream();
                s.ReadTimeout = 1500;
                s.WriteTimeout = 1500;
                _client = c;
                _stream = s;
            }
            catch (Exception ex)
            {
                try { c.Close(); } catch { }
                if (ex is TimeoutException) throw;
                throw new InvalidOperationException(
                    string.Format("无法连接 OnStep WiFi（{0}:{1}）：{2}", Host, Port, ex.Message), ex);
            }
        }

        /// <summary>排空接收缓冲区内残留数据。</summary>
        public void FlushPending()
        {
            lock (_sync)
            {
                if (_stream == null) return;
                try
                {
                    byte[] buf = new byte[256];
                    DateTime deadline = DateTime.UtcNow.AddMilliseconds(200);
                    while (DateTime.UtcNow < deadline && _stream.DataAvailable)
                    {
                        if (_stream.Read(buf, 0, buf.Length) <= 0) break;
                    }
                }
                catch { /* 超时或连接断开，忽略 */ }
            }
        }

        /// <summary>发送原始字节（TCP 流式，直接写入）。</summary>
        public void Write(byte[] data)
        {
            if (data == null) throw new ArgumentNullException("data");
            lock (_sync)
            {
                EnsureOpen();
                _stream.Write(data, 0, data.Length);
                _stream.Flush();
            }
        }

        /// <summary>
        /// 读取数据直到遇到 '#' 或超时。返回的字符串包含结尾 '#'。
        /// 若超时且没有任何数据，抛 TimeoutException。
        /// </summary>
        public string ReadUntilHash(int timeoutMs = 1500)
        {
            StringBuilder sb = new StringBuilder();
            byte[] buf = new byte[256];
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            lock (_sync)
            {
                EnsureOpen();
                while (DateTime.UtcNow < deadline)
                {
                    int rd = 0;
                    try { rd = _stream.Read(buf, 0, buf.Length); }
                    catch (TimeoutException) { break; }
                    catch (IOException) { break; }
                    catch (ObjectDisposedException) { break; }
                    if (rd <= 0) break;

                    for (int i = 0; i < rd; i++)
                    {
                        char c = (char)buf[i];
                        if (c == '\0') continue;
                        sb.Append(c);
                        if (c == '#') return sb.ToString();
                    }
                }
            }

            if (sb.Length == 0)
                throw new TimeoutException(string.Format("WiFi 读取超时：未收到任何应答。请确认 OnStep WiFi 模块的 TCP 端口 {0} 上就是原始命令转发（而非 HTTP/网页），且望远镜处于就绪状态。", Port));
            return sb.ToString();
        }

        private void EnsureOpen()
        {
            if (_stream == null) throw new InvalidOperationException("WiFi 未连接，请先建立连接。");
        }

        public void Close()
        {
            lock (_sync)
            {
                if (_stream != null) { try { _stream.Dispose(); } catch { } _stream = null; }
                if (_client != null) { try { _client.Close(); } catch { } _client = null; }
            }
        }

        public void Dispose() { Close(); }
    }
}
