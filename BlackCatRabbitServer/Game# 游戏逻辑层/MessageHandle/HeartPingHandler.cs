
namespace BlackCatRabbitServer
{
    // 心跳处理器实现
    public class HeartPingHandler : IMessageHandler<C2S_HeartPing>
    {
        public Task Handle(Session tempSession, C2S_HeartPing msg)
        { 
            // 回复心跳响应，带回客户端时间戳和服务端时间戳
            var pong = new S2C_HeartPong
            {
                ClientTimestamp = msg.ClientTimestamp,
                ServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                OnlineCount = SessionManager.Instance.Count,
                ServerLoad = 0 // 可后续接入实际负载
            };
            //Console.WriteLine($"[心跳接收] 时间戳: {msg.ClientTimestamp}，登录认证权限：{tempSession.IsAuthenticated}");
            tempSession.Send(pong);
            return Task.CompletedTask;
        }
    }
}
