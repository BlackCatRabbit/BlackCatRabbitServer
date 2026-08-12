



using K4os.Hash.xxHash;

namespace BlackCatRabbitServer
{
    // 登录处理器实现
    public class LoginHandler : IMessageHandler<C2S_Login>
    {
        private readonly AccountManager _accountManager = new();
        private readonly PlayersDBManager _playersDBManager = new();
        private readonly CharacterDBManager _characterDBManager = new();

        public Task Handle(Session tempSession, C2S_Login msg)
        {
            Console.WriteLine($"[登录请求] 用户名: {msg.UserName}");

            try
            {
                // 1. 校验登录
                var (ok, error, account) = _accountManager.VerifyLogin(msg.UserName, msg.Password);
                if (!ok)
                {
                    tempSession.Send(new S2C_LoginResult
                    {
                        Success = false,
                        AccountId = 0,
                        ErrorMsg = error
                    });
                    Console.WriteLine($"[登录] {msg.UserName} 登录失败: {error}");
                    return Task.CompletedTask;
                }

                // 2. 标记已认证
                tempSession.IsAuthenticated = true;


                //登陆完毕 服务端默认创建一个玩家和角色，后续可以扩展为多角色选择
                // 4. 获取或创建 Player
                var players = _playersDBManager.GetPlayersByAccountId(account.AccountId);
                Player player;
                if (players == null || players.Count == 0)
                {
                    player = new Player
                    {
                        PlayerId = IdGenerator.NextAccountId(),
                        AccountId = account.AccountId,
                        Name = account.UserName,                // 默认用账号名
                        OwnedCharacterIds = "10001",
                        Level = 1,
                    };
                    _playersDBManager.AddPlayer(player);
                    Console.WriteLine($"[登录] 为新账号创建玩家 PlayerId={player.PlayerId}");
                }
                else
                {
                    player = players[0];
                }

                // 5. 获取或创建该玩家的角色
                var characters = _characterDBManager.GetCharactersByPlayerId(player.PlayerId);
                Character character;
                if (characters == null || characters.Count == 0)
                {
                    character = new Character
                    {
                        CharacterId = IdGenerator.NextAccountId(),
                        PlayerId = player.PlayerId,
                        CharacterTemplateId = 10001,
                        SkinId = 1,
                        LastSceneId = 1,
                        PosX = 0f, PosY = 1.5f, PosZ = 0f,
                        RotX = 0f, RotY = 0f, RotZ = 0f,
                    };
                    _characterDBManager.AddCharacter(character);
                    Console.WriteLine($"[登录] 为新玩家创建角色 CharacterId={character.CharacterId}");
                }
                else
                {
                    character = characters[0];
                }

        /*--------------------------------默认创建end---------------------------------------------------------------*/
                // 6. 保存到 Session（服务器内部使用）数据库保存的上一次现场位置
                tempSession.AccountId = account.AccountId;
                tempSession.PlayerId = player.PlayerId;
                tempSession.PlayerName = player.Name;
                tempSession.CharacterTemplateId = character.CharacterTemplateId;
                tempSession.SkinId = character.SkinId;
                tempSession.SceneId = character.LastSceneId;
                tempSession.Position = new PVector3 { X = character.PosX, Y = character.PosY, Z = character.PosZ };
                tempSession.Rotation = new PVector3 { X = character.RotX, Y = character.RotY, Z = character.RotZ };

                // 注册 PlayerId → Session 索引（AOI O(1) 查找）
                SessionManager.Instance.MapPlayerSession(player.PlayerId, tempSession);
                // 3. 发送登录成功
                tempSession.Send(new S2C_LoginResult
                {
                    Success = true,
                    AccountId = account.AccountId
                });

        /*            // 7. 发送角色数据给客户端
                    tempSession.Send(new S2C_CharacterDataResult
                    {
                        PlayerId = player.PlayerId,
                        CurrentCharacterId = character.CharacterTemplateId,
                        SkinId = character.SkinId,
                        LastSceneId = character.LastSceneId,
                        Name = player.Name,
                        Position = new PVector3 { X = character.PosX, Y = character.PosY, Z = character.PosZ },
                        Rotation = new PVector3 { X = character.RotX, Y = character.RotY, Z = character.RotZ },
                    });*/

                Console.WriteLine($"[登录] {msg.UserName} 登录成功, SessionId={tempSession.Id}, AccountId={account.AccountId}, PlayerId={player.PlayerId}, CharacterTemplateId={character.CharacterTemplateId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[登录] {msg.UserName} 登录异常（DB操作失败）: {ex}");
                tempSession.Send(new S2C_LoginResult
                {
                    Success = false,
                    AccountId = 0,
                    ErrorMsg = "服务器内部错误，请稍后重试"
                });
            }

            return Task.CompletedTask;
        }
    }
}

