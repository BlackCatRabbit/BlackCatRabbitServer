using System;
using System.Security.Cryptography;
using System.Text;

namespace BlackCatRabbitServer
{
    /// <summary>
    /// 账号管理业务层，封装 AccountDBManager，
    /// 负责注册/登录时的密码哈希、参数校验等业务逻辑
    /// </summary>
    public class AccountManager
    {
        private readonly AccountDBManager _db = new();

        // ────────── 密码规则 ──────────
        private const int MinPasswordLength = 6;
        private const int MaxPasswordLength = 32;
        private const int MinUserNameLength = 2;
        private const int MaxUserNameLength = 20;

        /// <summary>
        /// 对明文密码做 SHA256 哈希（实际项目可加盐）
        /// </summary>
        public static string HashPassword(string plain)
        {
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(plain));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// 根据用户名查账号
        /// </summary>
        public Account GetAccountByUsername(string userName)
        {
            return _db.GetAccountByUsername(userName);
        }

        /// <summary>
        /// 根据账号ID查账号
        /// </summary>
        public Account GetAccountById(long accountId)
        {
            return _db.GetAccountById(accountId);
        }

        /// <summary>
        /// 注册新账号
        /// 返回 (是否成功, 错误消息, 新账号对象)
        /// </summary>
        public (bool Ok, string Error, Account Account) Register(string userName, string password)
        {
            // 1. 校验用户名
            if (string.IsNullOrWhiteSpace(userName))
                return (false, "用户名不能为空", null);

            string trimmedName = userName.Trim();
            if (trimmedName.Length < MinUserNameLength)
                return (false, $"用户名至少需要 {MinUserNameLength} 个字符", null);
            if (trimmedName.Length > MaxUserNameLength)
                return (false, $"用户名不能超过 {MaxUserNameLength} 个字符", null);

            // 2. 校验密码
            if (string.IsNullOrEmpty(password))
                return (false, "密码不能为空", null);
            if (password.Length < MinPasswordLength)
                return (false, $"密码至少需要 {MinPasswordLength} 个字符", null);
            if (password.Length > MaxPasswordLength)
                return (false, $"密码不能超过 {MaxPasswordLength} 个字符", null);

            // 3. 检查用户名是否已存在
            var existing = _db.GetAccountByUsername(trimmedName);
            if (existing != null)
                return (false, "用户名已被注册", null);

            // 4. 生成唯一账号ID（Snowflake算法），检测冲突后赋值
            const int maxRetry = 5;
            long accountId = 0;
            for (int i = 0; i < maxRetry; i++)
            {
                accountId = IdGenerator.NextAccountId();
                if (_db.GetAccountById(accountId) == null)
                    break;
                Console.WriteLine($"[注册] AccountId={accountId} 已存在，重试第 {i + 1} 次");
                accountId = 0;
            }
            if (accountId == 0)
                return (false, "服务器繁忙，请稍后重试", null);

            var account = new Account
            {
                AccountId = accountId,
                UserName  = trimmedName,
                Password  = HashPassword(password),
                CreatedAt = DateTime.Now
            };

            try
            {
                _db.AddAccount(account);
                Console.WriteLine($"[注册] 新账号创建成功: {trimmedName}, AccountId={account.AccountId}");
                return (true, null, account);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[注册] 数据库异常: {ex.Message}");
                return (false, "服务器内部错误，请稍后重试", null);
            }
        }

        /// <summary>
        /// 登录校验：验证用户名和密码
        /// 返回 (是否成功, 错误消息, 账号对象)
        /// </summary>
        public (bool Ok, string Error, Account Account) VerifyLogin(string userName, string password)
        {
            if (string.IsNullOrWhiteSpace(userName))
                return (false, "用户名不能为空", null);
            if (string.IsNullOrEmpty(password))
                return (false, "密码不能为空", null);

            var account = _db.GetAccountByUsername(userName.Trim());
            if (account == null)
                return (false, "账号不存在", null);

            string hashed = HashPassword(password);
            if (!string.Equals(account.Password, hashed, StringComparison.Ordinal))
                return (false, "密码错误", null);

            return (true, null, account);
        }
    }
}
