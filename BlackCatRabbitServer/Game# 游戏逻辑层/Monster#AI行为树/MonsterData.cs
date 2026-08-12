using System.Collections.Generic;

namespace BlackCatRabbitServer
{
    /// <summary>
    /// 怪物模板配置
    /// </summary>
    public class MonsterTemplate
    {
        public int TemplateId;
        public string Name;
        public int SceneId;
        public PVector3 Position;
        public PVector3 Rotation;
        public float MaxHP;
        public float SearchRange = 20f;
        public float ViewHalfAngle = 90f;
        public float MoveSpeed = 3f;
        public float MaxRoamDistance = 25f;
        public float StopDistance = 1.5f;

        public float AttackChance = 0.15f;
        public float AttackCD = 1.5f;
        public float AttackCastTime = 0.4f;
        public float AttackRecoveryTime = 0.6f;
        public float AttackRange = 2f;
        public float AttackDamage = 10f;

        public float DodgeChance = 0.1f;
        public float DodgeCD = 2f;
        public float DodgeCastTime = 0.3f;
        public float DodgeRecoveryTime = 0.4f;
        public float DodgeDistance = 3f;

        public float HitRecoveryTime = 0.2f;

        public float RangedSkillCD = 5f;
        public float RangedSkillCastTime = 0.3f;
        public float RangedSkillRecoveryTime = 0.6f;
        public float RangedSkillRangeMin = 7f;
        public float RangedSkillDamage = 15f;

        public float SkillAttackCD = 3f;
        public float SkillAttackCastTime = 0.8f;
        public float SkillAttackRecoveryTime = 0.8f;
        public float SkillAttackRange = 5f;
        public float SkillAttackDamage = 25f;
        public float SkillAttackDashDistance = 4.5f;
    }

    /// <summary>
    /// 怪物静态配置表（按场景索引）
    /// </summary>
    public static class MonsterConfig
    {
        private static readonly Dictionary<int, List<MonsterTemplate>> _sceneMonsters = new();

        static MonsterConfig()
        {
            // ========== 场景1 ==========
            _sceneMonsters[1] = new List<MonsterTemplate>
            {
                new MonsterTemplate
                {
                    TemplateId = 1001, Name = "史莱姆", SceneId = 1,
                    Position = new PVector3 { X = -5, Y = 0.2f, Z = 77 },
                    Rotation = new PVector3 { X = 0, Y = 0, Z = 0 },
                    MaxHP = 80000
                },
/*                new MonsterTemplate
                {
                    TemplateId = 1003, Name = "骷髅兵", SceneId = 1,
                    Position = new PVector3 { X = -4, Y = 0.2f, Z = 77 },
                    Rotation = new PVector3 { X = 0, Y = 0, Z = 0 },
                    MaxHP = 80000
                },*/
/*                new MonsterTemplate
                {
                    TemplateId = 1003, Name = "骷髅兵", SceneId = 1,
                    Position = new PVector3 { X = -6, Y = 0.2f, Z = 77 },
                    Rotation = new PVector3 { X = 0, Y = 45, Z = 0 },
                    MaxHP = 80000
                },*/
/*                new MonsterTemplate
                {
                    TemplateId = 1004, Name = "Boss·亡灵骑士", SceneId = 1,
                    Position = new PVector3 { X = -3, Y = 0.2f, Z = 77 },
                    Rotation = new PVector3 { X = 0, Y = 135, Z = 0 },
                    MaxHP = 80000
                }*/
            };

            // ========== 场景2 ==========
            _sceneMonsters[2] = new List<MonsterTemplate>
            {
                new MonsterTemplate
                {
                    TemplateId = 1003, Name = "骷髅兵", SceneId = 2,
                    Position = new PVector3 { X = 10, Y = 0, Z = 10 },
                    Rotation = new PVector3 { X = 0, Y = 0, Z = 0 },
                    MaxHP = 200
                },
                new MonsterTemplate
                {
                    TemplateId = 1003, Name = "骷髅兵", SceneId = 2,
                    Position = new PVector3 { X = -20, Y = 0, Z = -15 },
                    Rotation = new PVector3 { X = 0, Y = 45, Z = 0 },
                    MaxHP = 200
                },
                new MonsterTemplate
                {
                    TemplateId = 1004, Name = "Boss·亡灵骑士", SceneId = 2,
                    Position = new PVector3 { X = 50, Y = 0, Z = 50 },
                    Rotation = new PVector3 { X = 0, Y = 135, Z = 0 },
                    MaxHP = 800
                }
            };
        }

        public static List<MonsterTemplate> GetByScene(int sceneId)
        {
            return _sceneMonsters.TryGetValue(sceneId, out var list)
                ? list
                : new List<MonsterTemplate>();
        }
    }
}
