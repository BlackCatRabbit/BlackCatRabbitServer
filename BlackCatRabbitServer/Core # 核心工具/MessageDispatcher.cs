using Google.Protobuf;

//消息分发器 委托
namespace BlackCatRabbitServer
{
    public class MessageDispatcher
    {
        private static readonly MessageDispatcher _instance = new();
        public static MessageDispatcher Instance => _instance;

        // 存储处理器：消息ID → 委托 (Session, IMessage)
        private readonly Dictionary<MessageId, Action<Session, IMessage>> _handlers = new();

        /// <summary>
        /// 注册泛型处理器，自动处理类型转换
        /// </summary>
        public void RegisterHandler<T>(MessageId msgId, Action<Session, T> handler) where T : IMessage
        {
            _handlers[msgId] = (session, msg) => handler(session, (T)msg);
        }

        /// <summary>
        /// 分发消息（由 Connection 调用）
        /// </summary>
        public void Dispatch(Session session, int msgId, IMessage message)
        {
            MessageId messageId = (MessageId)msgId; // 将 int 转为 MessageId 枚举
            if (_handlers.TryGetValue(messageId, out var handler))
            {
                try
                {
                    handler(session, message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Dispatcher] 处理消息 {msgId} 异常: {ex.Message}");
                    // 可根据需要断开 session
                }
            }
            else
            {
                Console.WriteLine($"[Dispatcher] 未注册的消息ID: {msgId}");
            }
        }
    }
}
