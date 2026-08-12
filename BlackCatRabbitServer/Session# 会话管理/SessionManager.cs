using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackCatRabbitServer
{
    public class SessionManager
    {
        private static readonly SessionManager _instance = new();
        public static SessionManager Instance => _instance;

        private readonly ConcurrentDictionary<long, Session> _sessions = new();

        // PlayerId → Session 索引，O(1) 查找（AOI 高频调用）
        private readonly ConcurrentDictionary<long, Session> _playerSessions = new();

        public Session CreateSession(Connection connection)
        {
            var session = new Session
            {
                Id = IdGenerator.Next(),
                Connection = connection,
                CreateTime = DateTime.UtcNow,
                LastHeartbeatTime = DateTime.UtcNow
            };
            connection.Session = session; // 双向绑定
            _sessions[session.Id] = session;
            return session;
        }

        public Session Get(long id) => _sessions.GetValueOrDefault(id);

        /// <summary>
        /// 将已存在的 Session 注册到管理器（用于登录时将临时 Session 升级为正式 Session）
        /// </summary>
        public void RegisterSession(Session session)
        {
            _sessions[session.Id] = session;
        }

        /// <summary>
        /// 移除 Session（同时清理 PlayerId 索引）
        /// </summary>
        public void Remove(long id)
        {
            if (_sessions.TryRemove(id, out var session) && session.PlayerId != 0)
            {
                _playerSessions.TryRemove(session.PlayerId, out _);
            }
        }

        /// <summary>
        /// 登录完成后绑定 PlayerId → Session 映射（供 AOI 高频 O(1) 查找）
        /// </summary>
        public void MapPlayerSession(long playerId, Session session)
        {
            _playerSessions[playerId] = session;
        }

        public int Count => _sessions.Count;
        public List<Session> GetAllSessions() => _sessions.Values.ToList();

        /// <summary>某场景是否没有任何已认证玩家</summary>
        public bool IsSceneEmpty(int sceneId)
        {
            foreach (var session in _sessions.Values)
            {
                if (session.IsAuthenticated && session.SceneId == sceneId)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 通过 PlayerId 查找 Session（O(1)，高频调用）
        /// </summary>
        public Session GetSessionByPlayerId(long playerId)
        {
            _playerSessions.TryGetValue(playerId, out var session);
            return session;
        }

    }
}
