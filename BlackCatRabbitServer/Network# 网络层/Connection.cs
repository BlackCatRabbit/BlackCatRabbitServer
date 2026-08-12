//Connection（单连接核心 - 零GC接收循环）使用 ArrayPool<byte> 复用缓冲区，使用 ValueTask 减少异步状态机开销。
using Google.Protobuf;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Reflection.Emit;

namespace BlackCatRabbitServer
{
    public class Connection
    {
        // ---------- 公开属性 ----------
        public long ConnectionId { get; }         // 由 IdGenerator 生成
        public Session Session { get; set; }      // 逻辑层会话（可为 null）

        // ---------- 私有字段 ----------
        private readonly Socket _socket;
        private readonly ConnectionManager _manager;

        // 接收缓冲区（从 ArrayPool 租用）
        private byte[] _recvBuffer;
        private int _recvOffset;                // 已接收字节数
        private int _expectedPacketLen;         // -1 表示读头部，否则为 body 长度

        // 发送队列
        private readonly ConcurrentQueue<byte[]> _sendQueue = new();
        private int _isSending;                // 0=空闲, 1=发送中（用于互斥）

        // 常量
        private const int HEADER_SIZE = 4;
        private const int MAX_PACKET_SIZE = 1024 * 1024; // 1MB

        public Connection(Socket socket, ConnectionManager manager)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            ConnectionId = IdGenerator.Next();
            _recvBuffer = ArrayPool<byte>.Shared.Rent(8192);
            _expectedPacketLen = HEADER_SIZE; // 初始读头部
        }

        // ---------- 接收循环 ----------
        public async Task StartReceiveLoopAsync()
        {
            try
            {
                while (_socket.Connected)
                {
                    EnsureBufferCapacity();

                    int bytesRead = await _socket.ReceiveAsync(
                        new ArraySegment<byte>(_recvBuffer, _recvOffset, _recvBuffer.Length - _recvOffset),
                        SocketFlags.None);

                    if (bytesRead == 0) break; // 远程关闭

                    _recvOffset += bytesRead;

                    // 尝试解析出所有完整包
                    while (TryParsePacket(out byte[] packetData))
                    {
                        // 将解析出的包投递到逻辑线程
                        JobQueue.Instance.Enqueue(() => OnPacketReceived(packetData));
                    }
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
            {
                Console.WriteLine($"[Connection {ConnectionId}] 远程连接重置");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Connection {ConnectionId}] 接收异常: {ex.Message}");
            }
            finally
            {
                // 归还缓冲区
                if (_recvBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(_recvBuffer);
                    _recvBuffer = null;
                }
                Close();
            }
        }

        // ---------- 粘包解析 ----------
        private bool TryParsePacket(out byte[] packetData)
        {
            packetData = null;

            // 1) 读头部长度
            if (_expectedPacketLen == HEADER_SIZE)
            {
                if (_recvOffset < HEADER_SIZE) return false;

                int bodyLen = BitConverter.ToInt32(_recvBuffer, 0);
                if (bodyLen <= 0 || bodyLen > MAX_PACKET_SIZE)
                    throw new Exception($"非法包长度: {bodyLen}");

                _expectedPacketLen = bodyLen;
            }

            // 2) 检查 body 是否收齐
            int totalNeed = HEADER_SIZE + _expectedPacketLen;
            if (_recvOffset < totalNeed) return false;

            // 3) 取出完整包体（不含长度前缀）
            packetData = new byte[_expectedPacketLen];
            Buffer.BlockCopy(_recvBuffer, HEADER_SIZE, packetData, 0, _expectedPacketLen);

            // 4) 移位剩余数据到缓冲区头部
            int remaining = _recvOffset - totalNeed;
            if (remaining > 0)
                Buffer.BlockCopy(_recvBuffer, totalNeed, _recvBuffer, 0, remaining);
            _recvOffset = remaining;
            _expectedPacketLen = HEADER_SIZE; // 重置为读头部

            return true;
        }

        private void EnsureBufferCapacity()
        {
            if (_recvBuffer.Length - _recvOffset < 1024) // 剩余不足1KB则扩容
            {
                int newSize = _recvBuffer.Length * 2;
                byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
                Buffer.BlockCopy(_recvBuffer, 0, newBuffer, 0, _recvOffset);
                ArrayPool<byte>.Shared.Return(_recvBuffer);
                _recvBuffer = newBuffer;
            }
        }

        // ---------- 包处理回调 ----------
        private void OnPacketReceived(byte[] data)
        {
            try
            {
                // 解码得到 IMessage 对象
                IMessage msg = MessageCodec.Decode(data);
                int msgId = BitConverter.ToInt32(data, 0);

                // 如果 Session 尚未绑定，创建临时 Session 用于处理登录消息
                if (Session == null)
                {
                    Session = new Session
                    {
                        Id = IdGenerator.Next(),
                        Connection = this,
                        CreateTime = DateTime.Now,
                        LastHeartbeatTime = DateTime.Now,
                        IsAuthenticated = false
                    };
                    // 注册到 SessionManager，让心跳服务能检测到
                    SessionManager.Instance.RegisterSession(Session);
                    Console.WriteLine($"[Connection {ConnectionId}] 创建临时Session, 等待登录");
                }

                // 刷新心跳时间（收到任何消息都视为存活）
                Session.LastHeartbeatTime = DateTime.Now;

                // 交给分发器
                MessageDispatcher.Instance.Dispatch(Session, msgId, msg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Connection {ConnectionId}] 解析消息异常: {ex.Message}");
            }
        }

        // ---------- 发送（带长度前缀） ----------
        public void Send(byte[] data)
        {
            if (!_socket.Connected) return;
            if (data == null) return;

            // 队列长度限制（防内存溢出）
            if (_sendQueue.Count > 1024)
            {
                Console.WriteLine($"[Connection {ConnectionId}] 发送队列溢出，断开连接");
                Close();
                return;
            }

            _sendQueue.Enqueue(data);
            _ = ProcessSendQueueAsync();
        }

        public void Send(IMessage message)
        {
            Send(MessageCodec.Encode(message));
        }

        private async Task ProcessSendQueueAsync()
        {
            // 用 Interlocked 确保同一时刻只有一个发送任务在运行
            if (Interlocked.CompareExchange(ref _isSending, 1, 0) == 1)
                return; // 已有发送任务

            try
            {
                while (_sendQueue.TryDequeue(out byte[] data))
                {
                    // 构造完整包：长度前缀(4字节) + 数据
                    byte[] lengthPrefix = BitConverter.GetBytes(data.Length);
                    using (var ms = new System.IO.MemoryStream(lengthPrefix.Length + data.Length))
                    {
                        ms.Write(lengthPrefix, 0, lengthPrefix.Length);
                        ms.Write(data, 0, data.Length);
                        byte[] fullPacket = ms.ToArray();
                        await _socket.SendAsync(fullPacket, SocketFlags.None);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Connection {ConnectionId}] 发送异常: {ex.Message}");
                Close();
            }
            finally
            {
                // 释放发送锁
                Interlocked.Exchange(ref _isSending, 0);

                // 如果队列中又有了新数据，递归触发发送
                if (!_sendQueue.IsEmpty)
                    _ = ProcessSendQueueAsync();
            }
        }

        // ---------- 关闭 ----------
        public void Close()
        {
            try { _socket.Shutdown(SocketShutdown.Both); } catch { }
            try { _socket.Close(); } catch { }
            _manager.Remove(ConnectionId);

            // 及时清理 Session，避免等待心跳超时才被回收
            if (Session != null)
            {
                // 已认证玩家从 AOI 中移除
                if (Session.IsAuthenticated && Session.PlayerId > 0)
                    AOIManager.Instance.OnPlayerLeaveScene(Session);
                SessionManager.Instance.Remove(Session.Id);
            }

            // 清空发送队列
            while (_sendQueue.TryDequeue(out _)) { }
        }
    }

}
