using System;
using System.Collections.Generic;

namespace BlackCatRabbitServer
{
    /// <summary>
    /// 怪物生命周期管理器（单例）
    /// </summary>
    public class MonsterManager
    {
        private static readonly MonsterManager _instance = new();
        public static MonsterManager Instance => _instance;

        private readonly Dictionary<long, MonsterEntity> _monsters = new();
        private readonly HashSet<int> _initializedScenes = new();

        /// <summary>怪物数据锁（主线程 HandleMonsterHurt 和怪物线程 Tick 同时访问 _monsters）</summary>
        private readonly object _lock = new();

        public MonsterEntity GetMonster(long monsterId)
        {
            _monsters.TryGetValue(monsterId, out var entity);
            return entity;
        }

        public Dictionary<long, MonsterEntity>.ValueCollection GetAll()
            => _monsters.Values;

        #region 场景怪物初始化

        public void EnsureSceneMonstersAndNotify(Session session)
        {
            int sceneId = session.SceneId;
            if (sceneId <= 0) return;

            if (_initializedScenes.Add(sceneId))
            {
                var templates = MonsterConfig.GetByScene(sceneId);
                foreach (var t in templates)
                {
                    SpawnMonsterInternal(t);
                }
                Console.WriteLine($"[Monster] 场景{sceneId} 初始化完成，生成 {templates.Count} 个怪物");
            }

            NotifyVisibleMonsters(session);
        }

        #endregion

        #region 场景清怪

        /// <summary>清空某场景所有怪物（无玩家时调用）</summary>
        public void ClearSceneMonsters(int sceneId)
        {
            if (!_initializedScenes.Contains(sceneId)) return;

            List<long> toRemove;
            lock (_lock)
            {
                toRemove = new List<long>();
                foreach (var kvp in _monsters)
                {
                    if (kvp.Value.SceneId == sceneId)
                        toRemove.Add(kvp.Key);
                }
            }

            foreach (var id in toRemove)
                DespawnMonster(id);

            _initializedScenes.Remove(sceneId);
            Console.WriteLine($"[Monster] 场景{sceneId} 无玩家，清除 {toRemove.Count} 个怪物");
        }

        #endregion

        #region AI Tick

        public void Tick(float deltaTime)
        {
            // 快照拷贝（锁内只做拷贝，不阻塞主线程的 HandleMonsterHurt）
            List<MonsterEntity> snapshot;
            lock (_lock)
            {
                snapshot = new List<MonsterEntity>(_monsters.Values);
            }

            foreach (var entity in snapshot)
            {
                if (entity.CurrentHP <= 0) continue;
                MonsterBrain.Tick(entity, deltaTime);
                BroadcastMonsterState(entity);
            }
        }

        private void BroadcastMonsterState(MonsterEntity entity)
        {
            float posDelta = MathF.Sqrt(
                (entity.Position.X - entity.LastSentX) * (entity.Position.X - entity.LastSentX) +
                (entity.Position.Z - entity.LastSentZ) * (entity.Position.Z - entity.LastSentZ));

            bool changed = posDelta > 0.2f
                || MathF.Abs(entity.Rotation.Y - entity.LastSentRotY) > 1f
                || entity.TargetPlayerId != entity.LastSentTargetId
                || entity.TargetPos.X != entity.LastSentTgtX
                || entity.TargetPos.Z != entity.LastSentTgtZ
                || entity.AIState != entity.LastSentState
                || MathF.Abs(entity.CurrentHP - entity.LastSentCurrentHP) > 0.5f;

            if (!changed) return;

            var (gx, gz) = AOIManager.WorldToGridPublic(entity.Position.X, entity.Position.Z);
            var playerIds = AOIManager.Instance.GetEntityIdsInNineGrid(
                entity.SceneId, gx, gz, AOIEntityType.Player);

            if (playerIds.Count == 0) return;

            var stateMsg = entity.ToStateMsg();
            foreach (var pid in playerIds)
                SessionManager.Instance.GetSessionByPlayerId(pid)?.Send(stateMsg);

            entity.LastSentX = entity.Position.X;
            entity.LastSentZ = entity.Position.Z;
            entity.LastSentRotY = entity.Rotation.Y;
            entity.LastSentTargetId = entity.TargetPlayerId;
            entity.LastSentTgtX = entity.TargetPos.X;
            entity.LastSentTgtZ = entity.TargetPos.Z;
            entity.LastSentState = entity.AIState;
            entity.LastSentCurrentHP = entity.CurrentHP;
        }

        #endregion

        #region 攻击包

        public void SendMonsterAttack(MonsterEntity entity, int damageDealt)
        {
            var targetSession = SessionManager.Instance.GetSessionByPlayerId(entity.TargetPlayerId);
            if (targetSession == null) return;

            var attackMsg = new S2C_SkillCast
            {
                CasterId = entity.MonsterId,
                CasterType = 1,
                SkillId = 0,
                TargetPos = new PVector3 { X = entity.TargetPos.X, Y = entity.TargetPos.Y, Z = entity.TargetPos.Z },
                TargetId = entity.TargetPlayerId,
                CasterPos = new PVector3 { X = entity.Position.X, Y = entity.Position.Y, Z = entity.Position.Z },
                CasterHp = (int)entity.CurrentHP,
                TargetHp = targetSession.CurrentHp,
                Damage = damageDealt
            };

            targetSession.Send(attackMsg);
            Console.WriteLine($"[AI攻击包] 怪物{entity.MonsterId}(HP={entity.CurrentHP}) → 目标{entity.TargetPlayerId}(HP={targetSession.CurrentHp}) 伤害={damageDealt}");
        }

        #endregion

        #region 怪物受伤处理（C2S）

        public void HandleMonsterHurt(long monsterId, int damage, long attackerId, PVector3 attackerPos)
        {
            lock (_lock)
            {
                if (!_monsters.TryGetValue(monsterId, out var entity))
                {
                    Console.WriteLine($"[MonsterHurt] 怪物不存在: {monsterId}");
                    return;
                }

                if (entity.CurrentHP <= 0)
                {
                    Console.WriteLine($"[MonsterHurt] 怪物{monsterId}已死亡，忽略伤害");
                    return;
                }

                entity.CurrentHP -= damage;
                entity.HurtFlag = true;
                //entity.TargetPlayerId = attackerId; // 切换仇恨目标为最后攻击者
                Console.WriteLine($"[MonsterHurt] 怪物{monsterId}(HP={entity.CurrentHP}/{entity.MaxHP}) 被PlayerId={attackerId}攻击 伤害={damage} HurtFlag已设置");
            }
        }

        #endregion

        #region 内部生成

        private void SpawnMonsterInternal(MonsterTemplate template)
        {
            var entity = new MonsterEntity
            {
                MonsterId = MonsterEntity.NextId(),
                TemplateId = template.TemplateId,
                Name = template.Name,
                SceneId = template.SceneId,
                Position = new PVector3 { X = template.Position.X, Y = template.Position.Y, Z = template.Position.Z },
                Rotation = new PVector3 { X = template.Rotation.X, Y = template.Rotation.Y, Z = template.Rotation.Z },
                SpawnPosition = new PVector3 { X = template.Position.X, Y = template.Position.Y, Z = template.Position.Z },
                MaxHP = template.MaxHP,
                CurrentHP = template.MaxHP,
                SearchRange = template.SearchRange,
                ViewHalfAngle = template.ViewHalfAngle,
                MoveSpeed = template.MoveSpeed,
                MaxRoamDistance = template.MaxRoamDistance,
                StopDistance = template.StopDistance,
                AttackChance = template.AttackChance,
                AttackCD = template.AttackCD,
                AttackCastTime = template.AttackCastTime,
                AttackRecoveryTime = template.AttackRecoveryTime,
                AttackRange = template.AttackRange,
                AttackDamage = template.AttackDamage,
                DodgeChance = template.DodgeChance,
                DodgeCD = template.DodgeCD,
                DodgeCastTime = template.DodgeCastTime,
                DodgeRecoveryTime = template.DodgeRecoveryTime,
                DodgeDistance = template.DodgeDistance,
                HitRecoveryTime = template.HitRecoveryTime,
                RangedSkillCD = template.RangedSkillCD,
                RangedSkillCastTime = template.RangedSkillCastTime,
                RangedSkillRecoveryTime = template.RangedSkillRecoveryTime,
                RangedSkillRangeMin = template.RangedSkillRangeMin,
                RangedSkillDamage = template.RangedSkillDamage,
                SkillAttackCD = template.SkillAttackCD,
                SkillAttackCastTime = template.SkillAttackCastTime,
                SkillAttackRecoveryTime = template.SkillAttackRecoveryTime,
                SkillAttackRange = template.SkillAttackRange,
                SkillAttackDamage = template.SkillAttackDamage,
                SkillAttackDashDistance = template.SkillAttackDashDistance,
            };

            lock (_lock)
            {
                _monsters[entity.MonsterId] = entity;
            }

            AOIManager.Instance.AddEntityToGrid(
                entity.SceneId, entity.MonsterId, AOIEntityType.Monster,
                entity.Position.X, entity.Position.Z);
        }

        #endregion

        #region 通知玩家视野内怪物

        private void NotifyVisibleMonsters(Session session)
        {
            int sceneId = session.SceneId;
            var (gx, gz) = AOIManager.WorldToGridPublic(
                session.Position.X, session.Position.Z);

            var visibleMonsterIds = AOIManager.Instance.GetEntityIdsInNineGrid(
                sceneId, gx, gz, AOIEntityType.Monster);

            int count = 0;
            lock (_lock)
            {
                foreach (var monId in visibleMonsterIds)
                {
                    if (_monsters.TryGetValue(monId, out var entity))
                    {
                        if (entity.CurrentHP <= 0) continue;
                        session.Send(entity.ToSpawnMsg());
                        count++;
                    }
                }
            }

            if (count > 0)
                Console.WriteLine($"[Monster] 通知 PlayerId={session.PlayerId} 视野内 {count} 个怪物");
        }

        #endregion

        #region 摧毁

        public void DespawnMonster(long monsterId)
        {
            MonsterEntity? entity;
            lock (_lock)
            {
                if (!_monsters.TryGetValue(monsterId, out entity))
                    return;
                _monsters.Remove(monsterId);
            }

            AOIManager.Instance.RemoveEntityFromGrid(
                entity.SceneId, monsterId,
                entity.Position.X, entity.Position.Z);

            var (gx, gz) = AOIManager.WorldToGridPublic(entity.Position.X, entity.Position.Z);
            var visiblePlayerIds = AOIManager.Instance.GetEntityIdsInNineGrid(
                entity.SceneId, gx, gz, AOIEntityType.Player);

            var despawnMsg = new S2C_MonsterDespawn { MonsterId = monsterId };
            foreach (var pid in visiblePlayerIds)
                SessionManager.Instance.GetSessionByPlayerId(pid)?.Send(despawnMsg);

            Console.WriteLine($"[Monster] 摧毁 {entity.Name}#{monsterId}");
        }

        #endregion
    }
}
