using System.Threading.Tasks;

namespace BlackCatRabbitServer
{
    /// <summary>
    /// 处理客户端攻击怪物的受伤上报（C2S）
    /// </summary>
    public class MonsterHurtHandler : IMessageHandler<C2S_MonsterHurt>
    {
        public Task Handle(Session session, C2S_MonsterHurt message)
        {
            if (!session.IsAuthenticated || session.PlayerId == 0)
                return Task.CompletedTask;

            if (session.SceneId <= 0)
                return Task.CompletedTask;

            MonsterManager.Instance.HandleMonsterHurt(
                message.MonsterId, message.Damage, session.PlayerId, message.AttackerPos);

            return Task.CompletedTask;
        }
    }
}
