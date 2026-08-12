using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackCatRabbitServer
{
    public enum MessageId : int
    {
        // 客户端→服务器 (100000+)
        C2S_HeartPing = 100001,
        C2S_Login = 100002,
        C2S_SignUp = 100003,
        C2S_EnterScene = 100004,
        C2S_Move = 100005,
        C2S_AnimSync = 100006,
        C2S_AttackMonster = 100007,
        C2S_MonsterHurt = 100008,
        C2S_AnimSyncResult = 100009,
        C2S_TwoPointSync = 100010,

        // 服务器→客户端 (200000+)
        S2C_HeartPong = 200001,
        S2C_LoginResult = 200002,
        S2C_SignUpResult = 200003,
        S2C_MoveResult = 200004,
        S2C_AoiEnter = 200005,
        S2C_AoiLeave = 200006,
        S2C_EnterSceneResult = 200007,


        S2C_MonsterSpawn = 200009,
        S2C_MonsterDespawn = 200010,
        S2C_MonsterMove = 200011,
        S2C_SkillCast = 200012,
        S2C_TwoPointSync = 200013,
    }
}
