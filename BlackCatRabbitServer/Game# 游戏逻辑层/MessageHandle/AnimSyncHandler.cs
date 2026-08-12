using System.Threading.Tasks;

namespace BlackCatRabbitServer
{
    /// <summary>
    /// 处理客户端动画同步上报，广播给 AOI 范围内的其他玩家
    /// </summary>
    public class AnimSyncHandler : IMessageHandler<C2S_AnimSync>
    {
        public Task Handle(Session session, C2S_AnimSync message)
        {
            if (!session.IsAuthenticated || session.PlayerId == 0)
                return Task.CompletedTask;

            if (session.SceneId <= 0)
                return Task.CompletedTask;

            // 确保 PlayerId 以服务端为准
            message.PlayerId = session.PlayerId;

            // 广播动画同步给视野内的其他玩家
            AOIManager.Instance.BroadcastAnimSync(session.PlayerId, message);

            return Task.CompletedTask;
        }
    }
}
