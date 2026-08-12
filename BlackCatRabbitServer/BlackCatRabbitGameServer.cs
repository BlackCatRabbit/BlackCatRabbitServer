using BlackCatRabbitServer;
using Google.Protobuf;

class BlackCatRabbitGameServer
{
    static async Task Main(string[] args)
    {
        // 1. 启动逻辑线程（JobQueue）
        JobQueue.Instance.Start();
        Console.WriteLine("[系统] 逻辑线程已启动");

        // 2. 注册所有消息处理器
        RegisterHandlers();
        Console.WriteLine("[系统] 消息处理器已注册");

        // 3. 启动游戏循环（AOI 状态同步，每 100ms）
        var gameLoop = new GameLoop();
        gameLoop.Start();
        Console.WriteLine("[系统] 游戏循环已启动");

        // 4. 启动网络服务器（自研 TcpServer）
        var server = new TcpServer(8888);
        server.Start();
        Console.WriteLine($"[系统] 网络服务器已启动，监听端口: 8888");

        // 5. 启动心跳检测服务
        HeartbeatService.Instance.Start();
        Console.WriteLine("[系统] 心跳检测服务已启动");

        // 6. 模拟客户端登录测试
/*        Console.WriteLine("=====================================");
        Console.WriteLine("  模拟客户端登录测试");
        Console.WriteLine("=====================================");
        await RunClientSimulation();*/

        // 6. 保持主线程运行
        Console.WriteLine("=====================================");
        Console.WriteLine("  按任意键停止服务器...");
        Console.WriteLine("=====================================");
        Console.ReadKey();

        // 7. 优雅关闭
        HeartbeatService.Instance.Stop();
        server.Stop();
        gameLoop.Stop();
        JobQueue.Instance.Stop();
        Console.WriteLine("[系统] 服务器已安全关闭");
    }


    //注册监听Dispatch
    static void RegisterHandlers()
    {
        var dispatcher = MessageDispatcher.Instance;

        // 心跳处理器
        var heartbeatHandler = new HeartPingHandler();
        dispatcher.RegisterHandler<C2S_HeartPing>(MessageId.C2S_HeartPing,
            (session, msg) => SafeHandle(heartbeatHandler, session, msg));

        // 注册处理器
        var signUpHandler = new SignUpHandler();
        dispatcher.RegisterHandler<C2S_SignUp>(MessageId.C2S_SignUp,
            (session, msg) => SafeHandle(signUpHandler, session, msg));

        // 登录处理器 
        var loginHandler = new LoginHandler();
        dispatcher.RegisterHandler<C2S_Login>(MessageId.C2S_Login,
            (tempSession, msg) => SafeHandle(loginHandler, tempSession, msg));

        // 进入场景处理器
        var enterSceneHandler = new EnterSceneHandler();
        dispatcher.RegisterHandler<C2S_EnterScene>(MessageId.C2S_EnterScene,
            (session, msg) => SafeHandle(enterSceneHandler, session, msg));

        // 移动处理器
        var moveHandler = new MoveHandler();
        dispatcher.RegisterHandler<C2S_Move>(MessageId.C2S_Move,
            (session, msg) => SafeHandle(moveHandler, session, msg));

        // 动画同步处理器
        var animSyncHandler = new AnimSyncHandler();
        dispatcher.RegisterHandler<C2S_AnimSync>(MessageId.C2S_AnimSync,
            (session, msg) => SafeHandle(animSyncHandler, session, msg));

        // 怪物受伤处理器（客户端攻击怪物上报）
        var monsterHurtHandler = new MonsterHurtHandler();
        dispatcher.RegisterHandler<C2S_MonsterHurt>(MessageId.C2S_MonsterHurt,
            (session, msg) => SafeHandle(monsterHurtHandler, session, msg));

        // 双点同步处理器（客户端上报两个坐标 → 广播视野内其他玩家）
        var twoPointSyncHandler = new TwoPointSyncHandler();
        dispatcher.RegisterHandler<C2S_TwoPointSync>(MessageId.C2S_TwoPointSync,
            (session, msg) => SafeHandle(twoPointSyncHandler, session, msg));

        // 可以继续添加心跳等其他处理器...
    }

    /// <summary>
    /// 安全调用 handler，确保 Task 异常不被静默吞掉
    /// </summary>
    static void SafeHandle<T>(IMessageHandler<T> handler, Session session, T msg) where T : class
    {
        try
        {
            var task = handler?.Handle(session, msg);
            // 如果 handler 返回 Task 且有异常，确保异常被观察到
            if (task != null)
            {
                task.ContinueWith(t =>
                {
                    if (t.IsFaulted && t.Exception != null)
                        Console.WriteLine($"[Handler] 异步异常: {t.Exception.InnerException}");
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Handler] 同步异常: {ex}");
        }
    }
}
