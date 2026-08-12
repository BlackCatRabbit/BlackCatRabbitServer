using System;
using System.Threading;
using System.Threading.Tasks;

namespace BlackCatRabbitServer
{
    /// <summary>
    /// 心跳检测服务：定时检查所有 Session 的最后心跳时间，超时断开
    /// </summary>
    public class HeartbeatService : IDisposable
    {
        private static readonly HeartbeatService _instance = new();
        public static HeartbeatService Instance => _instance;

        private CancellationTokenSource? _cts;
        private Task? _checkTask;

        // 配置参数
        private const int CHECK_INTERVAL_MS = 5000;        // 每5秒检查一次
        private const int HEARTBEAT_TIMEOUT_SEC = 30;      // 30秒无心跳视为超时
        private const int UNAUTH_TIMEOUT_SEC = 10;          // 未认证连接10秒超时（防恶意连接）

        public void Start()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            _checkTask = CheckLoopAsync(_cts.Token);
            Console.WriteLine($"[心跳服务] 已启动 (检查间隔:{CHECK_INTERVAL_MS}ms, 超时:{HEARTBEAT_TIMEOUT_SEC}s, 未认证超时:{UNAUTH_TIMEOUT_SEC}s)");
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _checkTask = null;
            Console.WriteLine("[心跳服务] 已停止");
        }

        private async Task CheckLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(CHECK_INTERVAL_MS, token);
                    CheckAllSessions();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[心跳服务] 检查异常: {ex.Message}");
                }
            }
        }

        private void CheckAllSessions()
        {
            var now = DateTime.Now;
            int timeoutCount = 0;

            // 获取所有 Session 的快照（避免遍历时修改集合）
            var sessions = SessionManager.Instance.GetAllSessions();

            foreach (var session in sessions)
            {
                double elapsedSec = (now - session.LastHeartbeatTime).TotalSeconds;

                // 未认证连接：更短的超时时间
                if (!session.IsAuthenticated)
                {
                    if (elapsedSec >= UNAUTH_TIMEOUT_SEC)
                    {
                        Console.WriteLine($"[心跳服务] 未认证Session {session.Id} 超时({elapsedSec:F0}s), 断开连接");
                        session.Connection?.Close();
                        SessionManager.Instance.Remove(session.Id);
                        timeoutCount++;
                    }
                    continue;
                }

                // 已认证连接：正常心跳超时
                if (elapsedSec >= HEARTBEAT_TIMEOUT_SEC)
                {
                    Console.WriteLine($"[心跳服务] Session {session.Id} 心跳超时({elapsedSec:F0}s), 断开连接");
                    //AOIManager.Instance.OnPlayerLeaveScene(session); // 从 AOI 九宫格移除
                    session.Connection?.Close();
                    SessionManager.Instance.Remove(session.Id);
                    timeoutCount++;
                }
            }

            if (timeoutCount > 0)
                Console.WriteLine($"[心跳服务] 本轮清理 {timeoutCount} 个超时连接, 当前在线: {SessionManager.Instance.Count}");
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
