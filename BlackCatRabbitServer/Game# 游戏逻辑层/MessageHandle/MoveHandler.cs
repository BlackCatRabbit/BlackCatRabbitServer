using System.Threading.Tasks;

namespace BlackCatRabbitServer
{
    /// <summary>
    /// 处理客户端移动上报（实时广播位置给 AOI 范围内的玩家）
    /// </summary>
    public class MoveHandler : IMessageHandler<C2S_Move>
    {
        public Task Handle(Session session, C2S_Move message)
        {
            if (!session.IsAuthenticated || session.PlayerId == 0)
                return Task.CompletedTask;

            if (session.SceneId <= 0)
                return Task.CompletedTask;

            // 更新Session位置
            session.Position = message.Position;
            session.Rotation = message.Rotation;

            // 实时广播位置给 AOI 范围内的其他玩家
            AOIManager.Instance.BroadcastPosition(session);

            return Task.CompletedTask;
        }
    }
}
