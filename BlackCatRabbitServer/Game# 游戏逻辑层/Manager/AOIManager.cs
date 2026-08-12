using System;
using System.Collections.Generic;

namespace BlackCatRabbitServer
{
    /// <summary>AOI 实体类型</summary>
    public enum AOIEntityType { Player, Monster }

    /// <summary>
    /// 九宫格 AOI（Area of Interest）管理器
    /// 将世界坐标映射到格子，每个玩家的视野 = 以自身所在格为中心的 3x3 九宫格
    /// 按场景隔离，不同场景的玩家互不可见
    /// 移动时通过新旧九宫格差集计算出进入/离开的玩家
    /// </summary>
    public class AOIManager
    {
        private static readonly AOIManager _instance = new();
        public static AOIManager Instance => _instance;

        /// <summary>格子边长（世界单位），AOI 可见范围约 1.5 格 ≈ 75 单位</summary>
        public const float GridSize = 50f;

        /// <summary>网格数据锁（主线程写，怪物线程读）</summary>
        private readonly object _gridLock = new();

        // ========== 格子 → 玩家映射（按场景隔离） ==========
        /// <summary>场景ID → (格子Key → 该格子内的PlayerId集合)</summary>
        private readonly Dictionary<int, Dictionary<long, HashSet<long>>> _gridToPlayers = new();

        // ========== 格子 → 怪物映射（按场景隔离） ==========
        private readonly Dictionary<int, Dictionary<long, HashSet<long>>> _gridToMonsters = new();

        // ========== 怪物格子缓存 ==========
        private readonly Dictionary<long, long> _monsterGridKey = new();

        // ========== 玩家状态缓存 ==========
        /// <summary>每个玩家当前所在的格子 Key</summary>
        private readonly Dictionary<long, long> _playerGridKey = new();

        /// <summary>每个玩家当前视野内的 PlayerId 集合</summary>
        private readonly Dictionary<long, HashSet<long>> _playerVisibleSet = new();

        #region 坐标 ↔ 格子转换

        /// <summary>
        /// 将世界坐标 (X, Z) 转为格子坐标
        /// </summary>
        public static (int gx, int gz) WorldToGrid(float x, float z)
        {
            int gx = (int)Math.Floor(x / GridSize);
            int gz = (int)Math.Floor(z / GridSize);
            return (gx, gz);
        }

        /// <summary>
        /// 将格子坐标编码为唯一 long Key（高位32位存X，低位32位存Z）
        /// </summary>
        public static long GridKey(int gx, int gz)
        {
            return ((long)gx << 32) | (uint)gz;
        }

        /// <summary>
        /// 获取以 (gx, gz) 为中心的九宫格（3x3）所有格子 Key 列表
        /// </summary>
        public static List<long> GetNineGridKeys(int gx, int gz)
        {
            var keys = new List<long>(9);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    keys.Add(GridKey(gx + dx, gz + dz));
                }
            }
            return keys;
        }

        #endregion

        #region 格子内玩家查询（按场景隔离）

        /// <summary>
        /// 获取指定场景、指定九宫格范围内的所有 PlayerId
        /// </summary>
        private HashSet<long> GetPlayerIdsInNineGrid(int sceneId, int gx, int gz)
        {
            var result = new HashSet<long>();
            lock (_gridLock)
            {
                if (!_gridToPlayers.TryGetValue(sceneId, out var sceneGrids))
                    return result;

                var nineKeys = GetNineGridKeys(gx, gz);
                foreach (var key in nineKeys)
                {
                    if (sceneGrids.TryGetValue(key, out var playerSet))
                    {
                        foreach (var pid in playerSet)
                            result.Add(pid);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 通过格子Key和场景ID获取九宫格内的PlayerId（用于跨格移动差集计算）
        /// </summary>
        private HashSet<long> GetPlayerIdsInNineGridByKey(int sceneId, long centerKey)
        {
            int gx = (int)(centerKey >> 32);
            int gz = (int)(uint)(centerKey & 0xFFFFFFFF);
            return GetPlayerIdsInNineGrid(sceneId, gx, gz);
        }

        #endregion

        #region 格子增删（按场景隔离）

        private void AddPlayerToGrid(int sceneId, long playerId, long gridKey)
        {
            lock (_gridLock)
            {
                if (!_gridToPlayers.TryGetValue(sceneId, out var sceneGrids))
                {
                    sceneGrids = new Dictionary<long, HashSet<long>>();
                    _gridToPlayers[sceneId] = sceneGrids;
                }

                if (!sceneGrids.TryGetValue(gridKey, out var set))
                {
                    set = new HashSet<long>();
                    sceneGrids[gridKey] = set;
                }
                set.Add(playerId);
            }
        }

        private void RemovePlayerFromGrid(int sceneId, long playerId, long gridKey)
        {
            lock (_gridLock)
            {
                if (!_gridToPlayers.TryGetValue(sceneId, out var sceneGrids))
                    return;

                if (sceneGrids.TryGetValue(gridKey, out var set))
                {
                    set.Remove(playerId);
                    if (set.Count == 0)
                        sceneGrids.Remove(gridKey);
                }

                if (sceneGrids.Count == 0)
                    _gridToPlayers.Remove(sceneId);
            }
        }

        #endregion

        #region 玩家进出系统

        /// <summary>
        /// 检查玩家是否已在 AOI 系统中
        /// </summary>
        public bool IsPlayerInAOI(long playerId)
        {
            return _playerGridKey.ContainsKey(playerId);
        }

        /// <summary>
        /// 玩家加入 AOI 系统（进入场景/登录时调用）
        /// </summary>
        public void OnPlayerEnterScene(Session session)
        {
            if (session == null || session.PlayerId == 0) return;

            long playerId = session.PlayerId;
            int sceneId = session.SceneId;
            var (gx, gz) = WorldToGrid(session.Position.X, session.Position.Z);
            long gridKey = GridKey(gx, gz);

            // 1. 添加到场景格子
            AddPlayerToGrid(sceneId, playerId, gridKey);
            _playerGridKey[playerId] = gridKey;

            // 2. 计算同场景九宫格内的玩家（已按场景隔离）
            var visibleNow = GetPlayerIdsInNineGrid(sceneId, gx, gz);
            visibleNow.Remove(playerId);
            _playerVisibleSet[playerId] = new HashSet<long>(visibleNow);

            // 3. 通知自己：视野内有谁
            if (visibleNow.Count > 0)
            {
                var enterList = BuildPlayerSessionList(visibleNow);
                session.Send(new S2C_AoiEnter { Players = { enterList } });
            }

            // 4. 通知视野内的其他人：我进来了（预编码，只序列化一次）
            var enterMsg = new S2C_AoiEnter { Players = { BuildPlayerSession(session) } };
            byte[] encodedEnter = MessageCodec.Encode(enterMsg);
            foreach (var otherPid in visibleNow)
            {
                var otherSession = SessionManager.Instance.GetSessionByPlayerId(otherPid);
                otherSession?.Send(encodedEnter);

                // 更新对方的可见集，把自己加入（保持服务端状态一致）
                if (_playerVisibleSet.TryGetValue(otherPid, out var otherVis))
                {
                    otherVis.Add(playerId);
                }
            }

            Console.WriteLine($"[AOI] PlayerId={playerId} 进入场景{sceneId} 格子({gx},{gz}), 视野内{visibleNow.Count}人");
        }

        /// <summary>
        /// 玩家离开 AOI 系统（断线/切换场景时调用）
        /// </summary>
        public void OnPlayerLeaveScene(Session session)
        {
            if (session == null || session.PlayerId == 0) return;

            long playerId = session.PlayerId;
            int sceneId = session.SceneId;

            // 1. 从场景格子移除
            if (_playerGridKey.TryGetValue(playerId, out long oldGrid))
            {
                RemovePlayerFromGrid(sceneId, playerId, oldGrid);
                _playerGridKey.Remove(playerId);
            }

            // 2. 通知视野内的其他人：我走了
            if (_playerVisibleSet.TryGetValue(playerId, out var oldVisible))
            {
                foreach (var otherPid in oldVisible)
                {
                    var otherSession = SessionManager.Instance.GetSessionByPlayerId(otherPid);
                    if (otherSession != null)
                    {
                        otherSession.Send(new S2C_AoiLeave { PlayerIds = { playerId } });

                        // 从对方的可见集中移除自己
                        if (_playerVisibleSet.TryGetValue(otherPid, out var otherVisible))
                        {
                            otherVisible.Remove(playerId);
                        }
                    }
                }
            }

            _playerVisibleSet.Remove(playerId);
            Console.WriteLine($"[AOI] PlayerId={playerId} 离开场景{sceneId} AOI系统");

            // 3. 场景无玩家则清除怪物
            if (SessionManager.Instance.IsSceneEmpty(sceneId))
                MonsterManager.Instance.ClearSceneMonsters(sceneId);
        }

        #endregion

        #region 移动时九宫格变化检测（核心）

        /// <summary>
        /// 玩家移动后调用，检测格子是否变化，计算进入/离开差集并广播
        /// </summary>
        public void OnPlayerMove(Session session)
        {
            if (session == null || session.PlayerId == 0) return;

            long playerId = session.PlayerId;
            int sceneId = session.SceneId;
            var (newGx, newGz) = WorldToGrid(session.Position.X, session.Position.Z);
            long newGridKey = GridKey(newGx, newGz);

            if (!_playerGridKey.TryGetValue(playerId, out long oldGridKey))
            {
                OnPlayerEnterScene(session);
                return;
            }

            // 格子没变，不需要触发进入/离开检测
            if (oldGridKey == newGridKey)
            {
                // 还在视野内的人：只广播位置（预编码，只序列化一次）
                if (_playerVisibleSet.TryGetValue(playerId, out var visibleSet) && visibleSet.Count > 0)
                {
                    var moveMsg = new S2C_MoveResult
                    {
                        PlayerId = playerId,
                        Position = session.Position,
                        Rotation = session.Rotation
                    };
                    byte[] encoded = MessageCodec.Encode(moveMsg);
                    foreach (var pid in visibleSet)
                    {
                        var other = SessionManager.Instance.GetSessionByPlayerId(pid);
                        other?.Send(encoded);
                    }
                }
                return;
            }

            // ---- 格子变化，计算差集 ----
            var oldNine = GetPlayerIdsInNineGridByKey(sceneId, oldGridKey);
            oldNine.Remove(playerId);

            // 更新格子映射
            RemovePlayerFromGrid(sceneId, playerId, oldGridKey);


            AddPlayerToGrid(sceneId, playerId, newGridKey);
            _playerGridKey[playerId] = newGridKey;

            var newNine = GetPlayerIdsInNineGrid(sceneId, newGx, newGz);
            newNine.Remove(playerId);

            var oldVisible = _playerVisibleSet.TryGetValue(playerId, out var ov) ? new HashSet<long>(ov) : new HashSet<long>();

            // 差集计算
            var entered = new HashSet<long>(newNine);
            entered.ExceptWith(oldVisible);

            var left = new HashSet<long>(oldVisible);
            left.ExceptWith(newNine);

            _playerVisibleSet[playerId] = new HashSet<long>(newNine);

            // ---- 通知自己 ----
            if (entered.Count > 0)
            {
                var enterList = BuildPlayerSessionList(entered);
                session.Send(new S2C_AoiEnter { Players = { enterList } });
            }
            if (left.Count > 0)
            {
                session.Send(new S2C_AoiLeave { PlayerIds = { left } });
            }

            // ---- 通知视野内其他人 ----
            var selfSession = BuildPlayerSession(session);
            byte[]? encodedSelfEnter = null; // 懒编码：有新进入者时才序列化

            foreach (var pid in entered)
            {
                var other = SessionManager.Instance.GetSessionByPlayerId(pid);
                if (other != null)
                {
                    if (encodedSelfEnter == null)
                        encodedSelfEnter = MessageCodec.Encode(new S2C_AoiEnter { Players = { selfSession } });
                    other.Send(encodedSelfEnter);
                    if (_playerVisibleSet.TryGetValue(pid, out var otherVis))
                    {
                        otherVis.Add(playerId);
                    }
                }
            }

            foreach (var pid in left)
            {
                var other = SessionManager.Instance.GetSessionByPlayerId(pid);
                if (other != null)
                {
                    other.Send(new S2C_AoiLeave { PlayerIds = { playerId } });
                    if (_playerVisibleSet.TryGetValue(pid, out var otherVis))
                    {
                        otherVis.Remove(playerId);
                    }
                }
            }

            // 还在视野内的人：只广播位置（预编码，只序列化一次）
            var stillInRange = new HashSet<long>(newNine);
            stillInRange.IntersectWith(oldVisible);
            if (stillInRange.Count > 0)
            {
                var moveMsg = new S2C_MoveResult
                {
                    PlayerId = playerId,
                    Position = session.Position,
                    Rotation = session.Rotation
                };
                byte[] encoded = MessageCodec.Encode(moveMsg);
                foreach (var pid in stillInRange)
                {
                    var other = SessionManager.Instance.GetSessionByPlayerId(pid);
                    other?.Send(encoded);
                }
            }
        }

        #endregion

        #region 广播移动（MoveHandler 实时调用 — 保留兼容）

        public void BroadcastPosition(Session self)
        {
            OnPlayerMove(self);
        }

        #endregion

        #region 广播动画同步

        /// <summary>
        /// 将动画同步消息广播给视野内的所有其他玩家
        /// </summary>
        public void BroadcastAnimSync(long playerId, C2S_AnimSync animMsg)
        {
            if (!_playerVisibleSet.TryGetValue(playerId, out var visibleSet) || visibleSet.Count == 0)
                return;

            var result = new C2S_AnimSyncResult
            {
                PlayerId = animMsg.PlayerId,
                AnimName = animMsg.AnimName,
                CrossFadeTime = animMsg.CrossFadeTime,
                AnimBlendX = animMsg.AnimBlendX,
                AnimBlendY = animMsg.AnimBlendY,
                AnimCode = animMsg.AnimCode,
                ClassIndex = animMsg.ClassIndex,
                TargetId = animMsg.TargetId,
            };
            byte[] encoded = MessageCodec.Encode(result);

            foreach (var pid in visibleSet)
            {
                var other = SessionManager.Instance.GetSessionByPlayerId(pid);
                other?.Send(encoded);
            }
        }

        /// <summary>
        /// 广播双点同步：将客户端的两个坐标点转发给视野内其他玩家
        /// </summary>
        public void BroadcastTwoPointSync(long playerId, C2S_TwoPointSync msg)
        {
            if (!_playerVisibleSet.TryGetValue(playerId, out var visibleSet) || visibleSet.Count == 0)
                return;

            var result = new S2C_TwoPointSync
            {
                PlayerId = msg.PlayerId,
                Point1 = msg.Point1,
                Point2 = msg.Point2,
            };
            byte[] encoded = MessageCodec.Encode(result);

            foreach (var pid in visibleSet)
            {
                var other = SessionManager.Instance.GetSessionByPlayerId(pid);
                other?.Send(encoded);
            }
        }

        #endregion

        #region 内部辅助

        private PlayerSession BuildPlayerSession(Session s)
        {
            return new PlayerSession
            {
                PlayerId = s.PlayerId,
                PlayerName = s.PlayerName ?? string.Empty,
                CharacterTemplateId = (int)s.CharacterTemplateId,
                SkinId = s.SkinId,
                SceneId = s.SceneId,
                Position = s.Position,
                Rotation = s.Rotation
            };
        }

        private List<PlayerSession> BuildPlayerSessionList(HashSet<long> playerIds)
        {
            var list = new List<PlayerSession>(playerIds.Count);
            foreach (var pid in playerIds)
            {
                var s = SessionManager.Instance.GetSessionByPlayerId(pid);
                if (s != null)
                {
                    list.Add(BuildPlayerSession(s));
                }
            }
            return list;
        }

        #endregion

        #region 怪物 AOI 管理（公开 API，供 MonsterManager 调用）

        /// <summary>MonsterManager 需要的公开版 WorldToGrid</summary>
        public static (int gx, int gz) WorldToGridPublic(float x, float z) => WorldToGrid(x, z);

        /// <summary>将实体加入九宫格（玩家或怪物）</summary>
        public void AddEntityToGrid(int sceneId, long entityId, AOIEntityType type, float x, float z)
        {
            var (gx, gz) = WorldToGrid(x, z);
            long gridKey = GridKey(gx, gz);

            if (type == AOIEntityType.Monster)
            {
                AddMonsterToGrid(sceneId, entityId, gridKey);
                _monsterGridKey[entityId] = gridKey;
            }
            else
            {
                AddPlayerToGrid(sceneId, entityId, gridKey);
                _playerGridKey[entityId] = gridKey;
            }
        }

        /// <summary>从九宫格移除实体</summary>
        public void RemoveEntityFromGrid(int sceneId, long entityId, float x, float z)
        {
            var (gx, gz) = WorldToGrid(x, z);
            long gridKey = GridKey(gx, gz);

            // 优先从怪物格子缓存找，找不到就当玩家处理
            if (_monsterGridKey.TryGetValue(entityId, out long mg))
            {
                RemoveMonsterFromGrid(sceneId, entityId, mg);
                _monsterGridKey.Remove(entityId);
            }
            else
            {
                RemovePlayerFromGrid(sceneId, entityId, gridKey);
                _playerGridKey.Remove(entityId);
            }
        }

        /// <summary>获取九宫格内指定类型的实体ID</summary>
        public HashSet<long> GetEntityIdsInNineGrid(int sceneId, int gx, int gz, AOIEntityType type)
        {
            var result = new HashSet<long>();
            var gridDict = type == AOIEntityType.Monster ? _gridToMonsters : _gridToPlayers;

            if (!gridDict.TryGetValue(sceneId, out var sceneGrids))
                return result;

            var nineKeys = GetNineGridKeys(gx, gz);
            foreach (var key in nineKeys)
            {
                if (sceneGrids.TryGetValue(key, out var set))
                {
                    foreach (var id in set)
                        result.Add(id);
                }
            }
            return result;
        }

        private void AddMonsterToGrid(int sceneId, long monsterId, long gridKey)
        {
            if (!_gridToMonsters.TryGetValue(sceneId, out var sceneGrids))
            {
                sceneGrids = new Dictionary<long, HashSet<long>>();
                _gridToMonsters[sceneId] = sceneGrids;
            }
            if (!sceneGrids.TryGetValue(gridKey, out var set))
            {
                set = new HashSet<long>();
                sceneGrids[gridKey] = set;
            }
            set.Add(monsterId);
        }

        private void RemoveMonsterFromGrid(int sceneId, long monsterId, long gridKey)
        {
            if (!_gridToMonsters.TryGetValue(sceneId, out var sceneGrids))
                return;
            if (sceneGrids.TryGetValue(gridKey, out var set))
            {
                set.Remove(monsterId);
                if (set.Count == 0)
                    sceneGrids.Remove(gridKey);
            }
            if (sceneGrids.Count == 0)
                _gridToMonsters.Remove(sceneId);
        }

        #endregion
    }
}
