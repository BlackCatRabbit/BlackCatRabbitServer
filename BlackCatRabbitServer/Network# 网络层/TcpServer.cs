using System.Net;
using System.Net.Sockets;
using System.Buffers;


namespace BlackCatRabbitServer
{
    public class TcpServer : IDisposable
    {
        public void Dispose()
        {
            Stop();
        }
        private readonly Socket _listener;
        private readonly int _backlog = 1024;
        private bool _running;
        private readonly ConnectionManager _connManager;
        public TcpServer(int port)
        {
            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listener.Bind(new IPEndPoint(IPAddress.Any, port));
            _connManager = new ConnectionManager();
        }
        public void Start()
        {
            _running = true;
            _listener.Listen(_backlog);
            Console.WriteLine($"[自研] 服务器启动，端口: {((IPEndPoint)_listener.LocalEndPoint).Port}");
            // 启动 Accept 循环（单线程接受，也可多线程，但通常单线程足矣）
            _ = AcceptLoopAsync();
        }
        private async Task AcceptLoopAsync()
        {
            while (_running)
            {
                try
                {
                    var socket = await _listener.AcceptAsync();
                    if (!_running) break;
                    // 为每个连接创建 Connection 对象，开始接收数据
                    var connection = new Connection(socket, _connManager);
                    _connManager.Add(connection);
                    // 点火启动接收循环（不等待，避免阻塞 Accept）
                    _ = connection.StartReceiveLoopAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Accept异常] {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            _running = false;
            _listener.Close();
            _connManager.CloseAll();
        }
 
    }
}
