using System.Security.Principal;
using System.Threading.Tasks;

namespace BlackCatRabbitServer
{
    /// <summary>
    /// 处理客户端进入场景请求（集成九宫格 AOI）
    /// </summary>
    public class EnterSceneHandler : IMessageHandler<C2S_EnterScene>
    {
        public Task Handle(Session session, C2S_EnterScene message)
        {
            if (!session.IsAuthenticated || session.PlayerId == 0)
            {
                Console.WriteLine($"[EnterScene] 玩家未认证，拒绝进入场景 SessionId={session.Id}");
                return Task.CompletedTask;
            }

            int newSceneId = message.SceneId;
            if (newSceneId <= 0)
            {
                Console.WriteLine($"[EnterScene] 无效的场景ID: {newSceneId}");
                return Task.CompletedTask;
            }

            int oldSceneId = session.SceneId;
            bool isAlreadyInAoi = AOIManager.Instance.IsPlayerInAOI(session.PlayerId);

            // 已经在同一场景的 AOI 中，跳过
            if (isAlreadyInAoi && oldSceneId == newSceneId)
                return Task.CompletedTask;

            // 离开旧场景的 AOI（如果已在 AOI 中）
            if (isAlreadyInAoi && oldSceneId > 0)
            {
                AOIManager.Instance.OnPlayerLeaveScene(session);
            }

            // 更新场景
            session.SceneId = newSceneId;

            // 进入新场景的 AOI
            AOIManager.Instance.OnPlayerEnterScene(session);

            // 初始化场景怪物（首次进入时生成）并通知该玩家视野内怪物
            MonsterManager.Instance.EnsureSceneMonstersAndNotify(session);

            //主角的上次数据
            PlayerSession MyPlayerData = new PlayerSession
            {
                PlayerId = session.PlayerId,
                PlayerName = session.PlayerName ?? string.Empty,
                CharacterTemplateId = (int)session.CharacterTemplateId,
                SkinId = session.SkinId,
                SceneId = session.SceneId,
                Position = session.Position,
                Rotation = session.Rotation
            };
            session.Send(new S2C_EnterSceneResult
            {
                Success = true,
                Players = { MyPlayerData }
            });
            Console.WriteLine($"[EnterScene] PlayerId={session.PlayerId} 从场景{oldSceneId}切换到场景{newSceneId}, 已在AOI={isAlreadyInAoi}");
            return Task.CompletedTask;
        }
    }
}
