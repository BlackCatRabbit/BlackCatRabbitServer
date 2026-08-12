using System.Threading.Tasks;

namespace BlackCatRabbitServer
{
    /// <summary>
    /// 注册处理器：处理 C2S_SignUp 消息
    /// 完成注册后将账号信息返回客户端
    /// </summary>
    public class SignUpHandler : IMessageHandler<C2S_SignUp>
    {
        private readonly AccountManager _accountManager = new();

        public Task Handle(Session session, C2S_SignUp msg)
        {
            Console.WriteLine($"[注册请求] 用户名: {msg.UserName}");

            try
            {
                // 执行业务逻辑（在 JobQueue 线程执行）
                var (ok, error, account) = _accountManager.Register(msg.UserName, msg.Password);

                // 构造返回结果
                var result = new S2C_SignUpResult
                {
                    Success = ok,
                    ErrorMsg = ok ? "" : error
                };

                session.Send(result);

                Console.WriteLine(ok
                    ? $"[注册] {msg.UserName} 注册成功, AccountId={account.AccountId}"
                    : $"[注册] {msg.UserName} 注册失败: {error}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[注册] {msg.UserName} 注册异常: {ex}");
                session.Send(new S2C_SignUpResult
                {
                    Success = false,
                    ErrorMsg = "服务器内部错误，请稍后重试"
                });
            }

            return Task.CompletedTask;
        }
    }
}
