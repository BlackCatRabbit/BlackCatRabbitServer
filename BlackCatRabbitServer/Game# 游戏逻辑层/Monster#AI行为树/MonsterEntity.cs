using System.Threading;

namespace BlackCatRabbitServer
{
    /// <summary>
    /// 怪物AI状态
    /// </summary>
    public enum MonsterAIState
    {
        Idle,         // 待机
        Run,          // 追击/返回
        Attack,       // 攻击
        Dodge,        // 闪避
        Hit,          // 受伤
        SkillAttack,  // 技能
        RangedSkill,  // 远程技能
    }

    /// <summary>
    /// 怪物运行时实体（服务端权威）
    /// </summary>
    public class MonsterEntity
    {
        // ===== 身份 =====
        public long MonsterId;
        public int TemplateId;
        public string Name = string.Empty;
        public int SceneId;

        // ===== 位置 =====
        public PVector3 Position = new();
        public PVector3 Rotation = new();
        public PVector3 SpawnPosition = new();

        // ===== 血量 =====
        public float MaxHP;
        public float CurrentHP;

        // ===== 受伤 =====
        public bool HurtFlag;                     // 被打标记，行为树检测后清掉
        public float HitRecoveryTime = 0.2f;      // 受伤后摇时间（秒）
        public float HitRecoveryTimer;            // 受伤后摇计时器（前置中断用）

        // ===== 搜敌 =====
        public float SearchRange = 20f;           // 搜寻半径
        public float ViewHalfAngle = 90f;         // 视野半角（度），90=前方180°半扇形

        // ===== 移动 =====
        public float MoveSpeed = 3f;              // 移动速度（单位/秒）
        public float MaxRoamDistance = 25f;       // 最大游荡距离（超出此距离回出生点）
        public float StopDistance = 2f;           // 目标前方停步距离

        // ===== 攻击 =====
        public float AttackChance = 0.15f;        // 每帧触发概率（CD就绪时）
        public float AttackCD = 1.5f;             // 攻击冷却（秒）
        public float AttackCastTime = 0.4f;       // 攻击前摇（秒）
        public float AttackRecoveryTime = 0.3f;   // 攻击后摇（秒）
        public float AttackRange = 2.5f;          // 攻击距离
        public float AttackDamage = 10f;          // 攻击伤害
        public float AttackCooldownTimer;         // 当前CD剩余（≤0 即可攻击）

        // ===== 闪避 =====
        public float DodgeChance = 0.1f;          // 每帧触发概率（CD就绪时）
        public float DodgeCD = 2f;                // 闪避冷却（秒）
        public float DodgeCastTime = 0.3f;        // 施法前摇（秒）
        public float DodgeRecoveryTime = 0.2f;    // 闪避后摇（秒）
        public float DodgeDistance = 3f;          // 后撤距离
        public float DodgeCooldownTimer;          // 当前CD剩余（≤0 即可闪避）

        // ===== 远程技能 =====
        public float RangedSkillCD = 6f;          // 远程技能冷却（秒）
        public float RangedSkillCastTime = 0.6f;  // 远程技能前摇（秒）
        public float RangedSkillRecoveryTime = 0.4f; // 远程技能后摇（秒）
        public float RangedSkillRangeMin = 7f;    // 最小距离阈值（> 此距离才触发）
        public float RangedSkillDamage = 15f;     // 远程技能伤害
        public float RangedSkillCooldownTimer;    // 当前CD剩余

        // ===== 技能攻击（近战） =====
        public float SkillAttackCD = 4f;          // 技能攻击冷却（秒）
        public float SkillAttackCastTime = 0.8f;  // 技能攻击前摇（秒）
        public float SkillAttackRecoveryTime = 0.8f; // 技能攻击后摇（秒）
        public float SkillAttackRange = 5f;       // 技能攻击距离
        public float SkillAttackDamage = 25f;     // 技能攻击伤害
        public float SkillAttackDashDistance = 4.5f; // 技能攻击突进距离
        public float SkillAttackCooldownTimer;    // 当前CD剩余

        // ===== 行为树 =====
        public BTBlackboard Blackboard = new();

        // ===== AI 状态 =====
        public MonsterAIState AIState = MonsterAIState.Idle;
        public long TargetPlayerId;
        public PVector3 TargetPos = new();

        // ===== 上次发送的快照（对比后仅变化才发包） =====
        public float LastSentX, LastSentZ;
        public float LastSentRotY;
        public long LastSentTargetId;
        public float LastSentTgtX, LastSentTgtZ;
        public MonsterAIState LastSentState;
        public float LastSentCurrentHP;

        // ===== ID生成 =====
        private static long _nextId = 1000000;
        public static long NextId() => Interlocked.Increment(ref _nextId);

        // ===== 协议序列化 =====
        public S2C_MonsterSpawn ToSpawnMsg()
        {
            return new S2C_MonsterSpawn
            {
                MonsterId = MonsterId,
                TemplateId = TemplateId,
                Position = new PVector3 { X = Position.X, Y = Position.Y, Z = Position.Z },
                Rotation = new PVector3 { X = Rotation.X, Y = Rotation.Y, Z = Rotation.Z },
                MaxHP = MaxHP,
                CurrentHP = CurrentHP
            };
        }

        public S2C_MonsterMove ToStateMsg()
        {
            return new S2C_MonsterMove
            {
                MonsterId = MonsterId,
                CurrentHP = CurrentHP,
                TargetPlayerId = TargetPlayerId,
                TargetPos = TargetPos,
                MoveSpeed = (AIState == MonsterAIState.Run) ? MoveSpeed : 0f,
                Position = new PVector3 { X = Position.X, Y = Position.Y, Z = Position.Z },
                Rotation = new PVector3 { X = Rotation.X, Y = Rotation.Y, Z = Rotation.Z },
                AIState = (int)AIState
            };
        }
    }
}
