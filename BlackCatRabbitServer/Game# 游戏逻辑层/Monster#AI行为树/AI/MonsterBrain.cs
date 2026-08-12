using System;

namespace BlackCatRabbitServer
{
    public static class MonsterBrain
    {
        private static readonly BTSelector _root;
        private static readonly Random _rng = new();

        static MonsterBrain()
        {
            _root = new BTSelector("怪物AI根",

                // ──────────── 受伤（最高优先级，打断一切）────────────
                new BTSequence("受伤",
                    new BTCondition(bb =>
                    {
                        var e = bb.Get<MonsterEntity>("entity");
                        if (!e.HurtFlag) return false;
                        bb.Set("hitRecoveryElapsed", 0f);
                        return true;
                    }, "被打标记"),

                    new BTAction("受伤演出", (bb, dt) =>
                    {
                        var e = bb.Get<MonsterEntity>("entity");
                        SetState(e, MonsterAIState.Hit);
                        float elapsed = bb.Get<float>("hitRecoveryElapsed") + dt;
                        bb.Set("hitRecoveryElapsed", elapsed);
                        return elapsed >= e.HitRecoveryTime ? BTStatus.Success : BTStatus.Running;
                    }),

                    new BTAction(bb =>
                    {
                        var e = bb.Get<MonsterEntity>("entity");
                        e.HurtFlag = false;
                        bb.Remove("hitRecoveryElapsed");
                    }, "清除标记")
                ),

                // ──────────── 战斗 ────────────
                new BTReactiveSequence("战斗",
                    new BTCondition(
                        bb => bb.Get<MonsterEntity>("entity").TargetPlayerId != 0,
                        "有目标"),

                    new BTAction("验证目标", (bb, dt) =>
                    {
                        var e = bb.Get<MonsterEntity>("entity");

                        var session = SessionManager.Instance.GetSessionByPlayerId(e.TargetPlayerId);
                        if (session == null)
                        { e.TargetPlayerId = 0; return BTStatus.Failure; }

                        e.TargetPos = session.Position;
                        float dx = session.Position.X - e.Position.X;
                        float dz = session.Position.Z - e.Position.Z;
                        bb.Set("distToTarget", MathF.Sqrt(dx * dx + dz * dz));
                        return BTStatus.Success;
                    }),
                    new BTReactiveSelector("追击或停步",
                        // ── 停步 ──
                        new BTSequence("停步锁定",
                            new BTCondition(bb =>
                            {
                                var e = bb.Get<MonsterEntity>("entity");
                                return bb.Get<float>("distToTarget") <= e.StopDistance + 0.1f;
                            }, "在停步距离"),

                            // ── 战斗行为选择器（随机打乱顺序，防止固定优先级导致 Dodge↔SkillAttack 死循环）──
                            new BTSelector("战斗行为选择",
                                // ② TryDodge —— CD就绪 + 随机
                                new BTSequence("TryDodge",
                                    new BTCondition(bb =>
                                    {
                                        var e = bb.Get<MonsterEntity>("entity");
                                        if (e.DodgeCooldownTimer > 0) return false;
                                        if (_rng.NextDouble() >= e.DodgeChance) return false;
                                        bb.Set("dodgeCastElapsed", 0f);
                                        return true;
                                    }, "IsDodging"),
                                    new BTAction("DodgeCast", (bb, dt) =>
                                    {
                                        var e = bb.Get<MonsterEntity>("entity");
                                        SetState(e, MonsterAIState.Dodge);
                                        float elapsed = bb.Get<float>("dodgeCastElapsed") + dt;
                                        bb.Set("dodgeCastElapsed", elapsed);
                                        return elapsed >= e.DodgeCastTime ? BTStatus.Success : BTStatus.Running;
                                    }),
                                    new BTAction(bb =>
                                    {
                                        var e = bb.Get<MonsterEntity>("entity");
                                        float yawRad = e.Rotation.Y * (MathF.PI / 180f);
                                        float backX = -MathF.Sin(yawRad);
                                        float backZ = -MathF.Cos(yawRad);
                                        e.Position.X += backX * e.DodgeDistance;
                                        e.Position.Z += backZ * e.DodgeDistance;
                                        bb.Remove("dodgeCastElapsed");
                                        bb.Set("dodgeRecoveryElapsed", 0f);
                                        Console.WriteLine($"[AI闪避] 怪物{e.MonsterId} 后撤至 ({e.Position.X:F1},{e.Position.Z:F1}) CD={e.DodgeCD}s");
                                    }, "DoDodge"),
                                    new BTAction("DodgeRecovery", (bb, dt) =>
                                    {
                                        var e = bb.Get<MonsterEntity>("entity");
                                        float elapsed = bb.Get<float>("dodgeRecoveryElapsed") + dt;
                                        bb.Set("dodgeRecoveryElapsed", elapsed);
                                        if (elapsed >= e.DodgeRecoveryTime)
                                        {
                                            e.DodgeCooldownTimer = e.DodgeCD;
                                            bb.Remove("dodgeRecoveryElapsed");
                                            return BTStatus.Success;
                                        }
                                        return BTStatus.Running;
                                    })
                                ),
                                // ③ TrySkillAttack —— 近战技能+突进
                                new BTSequence("TrySkillAttack",
                                    new BTCondition(bb =>
                                    {
                                        var e = bb.Get<MonsterEntity>("entity");
                                        if (e.SkillAttackCooldownTimer > 0) return false;
                                        if (bb.Get<float>("distToTarget") > e.SkillAttackRange) return false;
                                        bb.Set("skillCastElapsed", 0f);
                                        return true;
                                    }, "IsSkillAttacking"),
                                    new BTAction("SkillCast", (bb, dt) =>
                                    {
                                        var e = bb.Get<MonsterEntity>("entity");
                                        LookAt(e, e.TargetPos);
                                        // 每帧刷新突进方向，跟随目标当前位置
                                        float dx = e.TargetPos.X - e.Position.X;
                                        float dz = e.TargetPos.Z - e.Position.Z;
                                        float mag = MathF.Sqrt(dx * dx + dz * dz);
                                        if (mag < 0.001f)
                                        {
                                            float yawRad = e.Rotation.Y * (MathF.PI / 180f);
                                            dx = MathF.Sin(yawRad);
                                            dz = MathF.Cos(yawRad);
                                            mag = 1f;
                                        }
                                        float invMag = 1f / mag;
                                        bb.Set("skillDashDirX", dx * invMag);
                                        bb.Set("skillDashDirZ", dz * invMag);
                                        bb.Set("skillDashDistToTarget", mag);

                                        SetState(e, MonsterAIState.SkillAttack);
                                        float elapsed = bb.Get<float>("skillCastElapsed") + dt;
                                        bb.Set("skillCastElapsed", elapsed);
                                        return elapsed >= e.SkillAttackCastTime ? BTStatus.Success : BTStatus.Running;
                                    }),
                                    new BTAction(bb =>
                                    {
                                        var e = bb.Get<MonsterEntity>("entity");
                                        float dashX = bb.Get<float>("skillDashDirX");
                                        float dashZ = bb.Get<float>("skillDashDirZ");
                                        float distToTarget = bb.Get<float>("skillDashDistToTarget");
                                        float dashDist;
                                        if (e.SkillAttackDashDistance > distToTarget)
                                            dashDist = distToTarget;
                                        else
                                            dashDist = distToTarget - 0.5f;
                                        e.Position.X += dashX * dashDist;
                                        e.Position.Z += dashZ * dashDist;
                                        Console.WriteLine($"[AI技能攻击] 怪物{e.MonsterId}(HP={e.CurrentHP}) 突进{dashDist:F1}格 (目标距{distToTarget:F1}) 至 ({e.Position.X:F1},{e.Position.Z:F1})");
                                    }, "SkillDash"),
                                    new BTAction(bb =>
                                    {
                                        var e = bb.Get<MonsterEntity>("entity");
                                        bb.Remove("skillCastElapsed");
                                        bb.Set("skillRecoveryElapsed", 0f);
                                    }, "DoSkillAttack"),
                                    new BTAction("SkillRecovery", (bb, dt) =>
                                    {
                                        var e = bb.Get<MonsterEntity>("entity");
                                        float elapsed = bb.Get<float>("skillRecoveryElapsed") + dt;
                                        bb.Set("skillRecoveryElapsed", elapsed);
                                        if (elapsed >= e.SkillAttackRecoveryTime)
                                        {
                                            e.SkillAttackCooldownTimer = e.SkillAttackCD;
                                            bb.Remove("skillRecoveryElapsed");
                                            return BTStatus.Success;
                                        }
                                        return BTStatus.Running;
                                    })
                                ),
                                // ④ TryAttack —— 普攻兜底
                                new BTSequence("TryAttack",
                                    new BTCondition(bb =>
                                    {
                                        var e = bb.Get<MonsterEntity>("entity");
                                        if (e.AttackCooldownTimer > 0) return false;
                                        if (bb.Get<float>("distToTarget") > e.AttackRange) return false;
                                        bb.Set("attackCastElapsed", 0f);
                                        return true;
                                    }, "可攻击"),
                                    new BTAction("攻击前摇", (bb, dt) =>
                                    {
                                        var e = bb.Get<MonsterEntity>("entity");
                                        SetState(e, MonsterAIState.Attack);
                                        LookAt(e, e.TargetPos);
                                        float elapsed = bb.Get<float>("attackCastElapsed") + dt;
                                        bb.Set("attackCastElapsed", elapsed);
                                        return elapsed >= e.AttackCastTime ? BTStatus.Success : BTStatus.Running;
                                    }),
                                    new BTAction(bb =>
                                    {
                                        var e = bb.Get<MonsterEntity>("entity");
                                        bb.Remove("attackCastElapsed");
                                        bb.Set("attackRecoveryElapsed", 0f);
                                    }, "造成伤害"),
                                    new BTAction("攻击后摇", (bb, dt) =>
                                    {
                                        var e = bb.Get<MonsterEntity>("entity");
                                        float elapsed = bb.Get<float>("attackRecoveryElapsed") + dt;
                                        bb.Set("attackRecoveryElapsed", elapsed);
                                        if (elapsed >= e.AttackRecoveryTime)
                                        {
                                            e.AttackCooldownTimer = e.AttackCD;
                                            bb.Remove("attackRecoveryElapsed");
                                            return BTStatus.Success;
                                        }
                                        return BTStatus.Running;
                                    })
                                ),
                                // ⑤ 兜底 —— 所有行为CD时原地等待，确保BTSelector永不返回Failure
                                new BTAction(bb =>
                                {
                                    var e = bb.Get<MonsterEntity>("entity");
                                    LookAt(e, e.TargetPos);
                                    SetState(e, MonsterAIState.Idle);
                                }, "兜底等待")
                            )
                        ),

                        // ── 远程攻击 ──
                        new BTSequence("TryRangedSkill",
                            new BTCondition(bb =>
                            {
                                var e = bb.Get<MonsterEntity>("entity");
                                if (e.RangedSkillCooldownTimer > 0) return false;
                                if (bb.Get<float>("distToTarget") <= e.RangedSkillRangeMin) return false;
                                bb.Set("rangedCastElapsed", 0f);
                                return true;
                            }, "IsRanged"),
                            new BTAction("RangedCast", (bb, dt) =>
                            {
                                var e = bb.Get<MonsterEntity>("entity");
                                SetState(e, MonsterAIState.RangedSkill);
                                LookAt(e, e.TargetPos);
                                float elapsed = bb.Get<float>("rangedCastElapsed") + dt;
                                bb.Set("rangedCastElapsed", elapsed);
                                return elapsed >= e.RangedSkillCastTime ? BTStatus.Success : BTStatus.Running;
                            }),
                            new BTAction(bb =>
                            {
                                var e = bb.Get<MonsterEntity>("entity");
                                bb.Remove("rangedCastElapsed");
                                bb.Set("rangedRecoveryElapsed", 0f);
                            }, "DoRangedSkill"),
                            new BTAction("RangedRecovery", (bb, dt) =>
                            {
                                var e = bb.Get<MonsterEntity>("entity");
                                float elapsed = bb.Get<float>("rangedRecoveryElapsed") + dt;
                                bb.Set("rangedRecoveryElapsed", elapsed);
                                if (elapsed >= e.RangedSkillRecoveryTime)
                                {
                                    e.RangedSkillCooldownTimer = e.RangedSkillCD;
                                    bb.Remove("rangedRecoveryElapsed");
                                    return BTStatus.Success;
                                }
                                return BTStatus.Running;
                            })
                        ),
                        // ── 追击 ──
                        new BTAction("追向目标", (bb, dt) =>
                        {
                            var e = bb.Get<MonsterEntity>("entity");
                            float d = bb.Get<float>("distToTarget");

                            // 已在停步距离内，不追击也不后退，交给"停步锁定"处理
                            if (d <= e.StopDistance + 0.1f)
                                return BTStatus.Failure;

                            // 超出漫游范围 → 停止追击返回出生点，但保留仇恨目标（回家后重新接战）
                            if (bb.Get<float>("distToSpawn") > e.MaxRoamDistance)
                                return BTStatus.Failure;

                            SetState(e, MonsterAIState.Run);
                            LookAt(e, e.TargetPos);

                            float dx = e.TargetPos.X - e.Position.X;
                            float dz = e.TargetPos.Z - e.Position.Z;
                            float step = e.MoveSpeed * dt;
                            float moveDist = d - e.StopDistance;
                            if (step >= moveDist)
                            {
                                e.Position.X += dx / d * moveDist;
                                e.Position.Z += dz / d * moveDist;
                            }
                            else
                            {
                                e.Position.X += dx / d * step;
                                e.Position.Z += dz / d * step;
                            }
                            return BTStatus.Running;
                        })
                    )


                ),
                // ──────────── 返回出生点 ────────────
                new BTReactiveSequence("返回出生点",
                    new BTCondition(
                        bb => bb.Get<float>("distToSpawn") > 1f,
                        "远离出生点"),
                    new BTAction("移向出生点", (bb, dt) =>
                    {
                        var e = bb.Get<MonsterEntity>("entity");
                        SetState(e, MonsterAIState.Run);
                        e.TargetPos = e.SpawnPosition;
                        MoveToward(e, e.SpawnPosition, dt);
                        return BTStatus.Running;
                    })
                ),

                // ──────────── 无目标时自动索敌 / 待机 ────────────
                new BTReactiveSequence("无目标搜敌",
                    new BTCondition(
                        bb => bb.Get<MonsterEntity>("entity").TargetPlayerId == 0,
                        "无目标"),
                    new BTAction(bb =>
                    {
                        var e = bb.Get<MonsterEntity>("entity");
                        var result = FindNearestInFOV(e);
                        if (result.HasValue)
                        {
                            e.TargetPlayerId = result.Value.playerId;
                            e.TargetPos = result.Value.position;
                            LookAt(e, result.Value.position);
                        }
                    }, "扇形搜敌"),
                    new BTAction(bb =>
                    {
                        var e = bb.Get<MonsterEntity>("entity");
                        SetState(e, MonsterAIState.Idle);
                    }, "待机")
                )
            );
        }

        // ==================== 每帧入口 ====================

        public static void Tick(MonsterEntity entity, float deltaTime)
        {
            if (entity.CurrentHP <= 0) return;

            // 攻击/闪避/远程/技能CD递减
            if (entity.AttackCooldownTimer > 0)
                entity.AttackCooldownTimer -= deltaTime;
            if (entity.DodgeCooldownTimer > 0)
                entity.DodgeCooldownTimer -= deltaTime;
            if (entity.RangedSkillCooldownTimer > 0)
                entity.RangedSkillCooldownTimer -= deltaTime;
            if (entity.SkillAttackCooldownTimer > 0)
                entity.SkillAttackCooldownTimer -= deltaTime;

            var bb = entity.Blackboard;
            bb.Set("entity", entity);
            bb.Set("deltaTime", deltaTime);

            // distToSpawn
            float mx = entity.Position.X, mz = entity.Position.Z;
            bb.Set("distToSpawn", Dist2D(mx, mz, entity.SpawnPosition.X, entity.SpawnPosition.Z));

            // distToTarget
            if (entity.TargetPlayerId != 0)
            {
                var session = SessionManager.Instance.GetSessionByPlayerId(entity.TargetPlayerId);
                if (session != null)
                    bb.Set("distToTarget", Dist2D(mx, mz, session.Position.X, session.Position.Z));
            }

            // ── 前置受伤中断：立刻响应，跳过行为树 ──
            if (entity.HurtFlag)
            {
                SetState(entity, MonsterAIState.Hit);
                entity.HitRecoveryTimer += deltaTime;
                if (entity.HitRecoveryTimer >= entity.HitRecoveryTime)
                {
                    entity.HurtFlag = false;
                    entity.HitRecoveryTimer = 0f;
                    // 强制 BroadcastMonsterState 本帧重新广播（退出Hit状态）
                    entity.LastSentState = (MonsterAIState)(-1);
                }
                return; // 不执行行为树
            }

            _root.Tick(bb, deltaTime);
        }

        // ==================== 工具 ====================

        private static void MoveToward(MonsterEntity entity, PVector3 target, float dt)
        {
            float dx = target.X - entity.Position.X;
            float dz = target.Z - entity.Position.Z;
            float dist = MathF.Sqrt(dx * dx + dz * dz);
            if (dist <= 0.01f) return;
            float step = entity.MoveSpeed * dt;
            if (step >= dist) { entity.Position.X = target.X; entity.Position.Z = target.Z; }
            else { entity.Position.X += dx / dist * step; entity.Position.Z += dz / dist * step; }
            LookAt(entity, target);
        }

        private static void LookAt(MonsterEntity entity, PVector3 target)
        {
            float dx = target.X - entity.Position.X;
            float dz = target.Z - entity.Position.Z;
            if (MathF.Abs(dx) < 0.01f && MathF.Abs(dz) < 0.01f) return;
            entity.Rotation.Y = MathF.Atan2(dx, dz) * (180f / MathF.PI);
        }

        private static float Dist2D(float x1, float z1, float x2, float z2)
            => MathF.Sqrt((x2 - x1) * (x2 - x1) + (z2 - z1) * (z2 - z1));

        /// <summary>搜索最近玩家：优先前方扇形视野，无结果则回退 360° 全范围搜索</summary>
        private static (long playerId, PVector3 position)? FindNearestInFOV(MonsterEntity entity)
        {
            float mx = entity.Position.X, mz = entity.Position.Z;
            var (gx, gz) = AOIManager.WorldToGridPublic(mx, mz);
            var playerIds = AOIManager.Instance.GetEntityIdsInNineGrid(entity.SceneId, gx, gz, AOIEntityType.Player);

            float yawRad = entity.Rotation.Y * (MathF.PI / 180f);
            float fx = MathF.Sin(yawRad), fz = MathF.Cos(yawRad);
            float cosTh = MathF.Cos(entity.ViewHalfAngle * (MathF.PI / 180f));

            long fovBestId = 0, allBestId = 0;
            float fovBestDist = entity.SearchRange, allBestDist = entity.SearchRange;
            PVector3 fovBestPos = null, allBestPos = null;

            foreach (var pid in playerIds)
            {
                var s = SessionManager.Instance.GetSessionByPlayerId(pid);
                if (s == null) continue;
                float dx = s.Position.X - mx, dz = s.Position.Z - mz;
                float dist = MathF.Sqrt(dx * dx + dz * dz);

                // 360° 全范围最近（作为回退）
                if (dist < allBestDist)
                { allBestDist = dist; allBestId = pid; allBestPos = s.Position; }

                // 前方扇形视野最近（优先）
                if (dist < fovBestDist && fx * dx + fz * dz >= dist * cosTh)
                { fovBestDist = dist; fovBestId = pid; fovBestPos = s.Position; }
            }

            if (fovBestId != 0) return (fovBestId, fovBestPos);
            if (allBestId != 0) return (allBestId, allBestPos);
            return null;
        }

        private static void SetState(MonsterEntity entity, MonsterAIState newState)
        {
            if (entity.AIState == newState) return;
            Console.WriteLine($"[AI状态] 怪物{entity.MonsterId} {entity.AIState} → {newState}");
            entity.AIState = newState;
        }
    }
}
