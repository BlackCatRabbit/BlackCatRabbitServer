using System.Threading.Tasks;

namespace BlackCatRabbitServer
{
    /// <summary>
    /// 处理客户端上报两个坐标点，广播给 AOI 范围内的其他玩家
    /// </summary>
    public class TwoPointSyncHandler : IMessageHandler<C2S_TwoPointSync>
    {
        public Task Handle(Session session, C2S_TwoPointSync message)
        {
            if (!session.IsAuthenticated || session.PlayerId == 0)
                return Task.CompletedTask;

            if (session.SceneId <= 0)
                return Task.CompletedTask;

            // 确保 PlayerId 以服务端为准
            message.PlayerId = session.PlayerId;

            // 广播双点同步给视野内的其他玩家（不含自己）
            AOIManager.Instance.BroadcastTwoPointSync(session.PlayerId, message);

            return Task.CompletedTask;
        }
    }
}
