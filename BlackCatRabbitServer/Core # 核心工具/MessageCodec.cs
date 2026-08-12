using Google.Protobuf;
using System;
using System.Collections.Generic;

//完整的编解码器
namespace BlackCatRabbitServer
{
    public static class MessageCodec
    {
        // 消息ID ↔ 类型映射
        private static readonly Dictionary<int, Type> _idToType = new();
        private static readonly Dictionary<Type, int> _typeToId = new();

        // 静态构造时注册所有协议（也可通过反射自动扫描）
        static MessageCodec()
        {
            // 示例：手动注册，实际项目可扫描程序集
/*            Register(1, typeof(C2S_Move));
            Register(2, typeof(S2C_StateSync));
            Register(3, typeof(S2C_PlayerEnter));
            Register(4, typeof(S2C_PlayerLeave));*/
            // ... 添加更多
            // 自动扫描程序集中所有实现了 IMessage 的类型
            var messageTypes = typeof(MessageCodec).Assembly.GetTypes()
                .Where(t => typeof(IMessage).IsAssignableFrom(t) && !t.IsAbstract)
                .ToList();

            int id = 0; // 可以约定消息ID = 自定义属性，或通过其他映射
            foreach (var type in messageTypes)
            {
                // 如果你的协议有约定，可以用自定义特性标记ID；这里简单用递增
                // 实际建议用特性 [MessageId(1)] 标记
                id = MessageIdMapper.GetValue(type.ToString());
                Register(id, type);
            }
        }

        public static void Register(int msgId, Type msgType)
        {
            if (!typeof(IMessage).IsAssignableFrom(msgType))
                throw new ArgumentException($"{msgType} 未实现 IMessage");

            _idToType[msgId] = msgType;
            _typeToId[msgType] = msgId;
        }

        /// <summary>
        /// 编码：消息ID(4字节) + 消息体(Protobuf序列化)
        /// </summary>
        public static byte[] Encode(IMessage message)
        {
            if (!_typeToId.TryGetValue(message.GetType(), out int msgId))
                throw new Exception($"未注册的消息类型: {message.GetType()}");

            byte[] body = message.ToByteArray();
            byte[] result = new byte[4 + body.Length];
            // 消息ID以小端序写入前4字节
            BitConverter.GetBytes(msgId).CopyTo(result, 0);
            body.CopyTo(result, 4);
            return result;
        }

        /// <summary>
        /// 解码：从完整的包体（不含长度前缀）中解析出 IMessage 对象
        /// </summary>
        public static IMessage Decode(byte[] data)
        {
            if (data.Length < 4)
                throw new Exception("数据过短，无法解析消息ID");

            int msgId = BitConverter.ToInt32(data, 0);
            if (!_idToType.TryGetValue(msgId, out Type type))
                throw new Exception($"未知消息ID: {msgId}");

            // 提取消息体（去掉前4字节ID）
            byte[] body = new byte[data.Length - 4];
            Buffer.BlockCopy(data, 4, body, 0, body.Length);

            // 创建消息实例并通过 MergeFrom 解析
            var message = (IMessage)Activator.CreateInstance(type);
            message.MergeFrom(body);
            return message;
        }

        /// <summary>
        /// 获取消息ID（用于分发时快速获取）
        /// </summary>
        public static int GetMessageId(IMessage message)
        {
            return _typeToId[message.GetType()];
        }
    }
}
