using Google.Protobuf;
using System;
using System.Collections.Concurrent;

namespace BlackCatRabbitServer
{
    public class Session
    {
        private PVector3 position;
        private PVector3 rotation;
        private string playerName;

        public long Id { get; set; }
        public Connection Connection { get; set; }

        // 心跳相关
        public DateTime LastHeartbeatTime { get; set; } = DateTime.Now;
        public DateTime CreateTime { get; set; } = DateTime.Now;
        public bool IsAuthenticated { get; set; }

        // 玩家相关
        public long AccountId { get; set; }
        public long PlayerId { get; set; }
        public string PlayerName { get => playerName; set => playerName = value; }

        // 角色相关
        public long CharacterTemplateId { get; set; }
        public int SkinId { get; set; }
        public int SceneId { get; set; }

        // 角色当前位置和朝向
        public PVector3 Position { get => position; set => position = value; }
        public PVector3 Rotation { get => rotation; set => rotation = value; }

        // 房间
        public int RoomId { get; set; }

        // 血量（服务器权威，怪物战斗结算扣血用）
        public int CurrentHp { get; set; } = 100;
        public int MaxHp { get; set; } = 100;

        public void Send(byte[] data) => Connection?.Send(data);
        public void Send(IMessage message) => Connection?.Send(message);
    }
}
