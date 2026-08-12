using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackCatRabbitServer
{
    public class AccountDBManager
    {
        /// <summary>
        /// 根据账号ID获取账号
        /// </summary>
        public Account GetAccountById(long accountId)
        {
            using (var session = NHibernateHelper.OpenSession())
            {
                var list = session.QueryOver<Account>().Where(x => x.AccountId == accountId).List();
                if (list != null && list.Count > 0)
                {
                    return list[0];
                }
                return null;
            }
        }

        /// <summary>
        /// 根据用户名获取账号
        /// </summary>
        public Account GetAccountByUsername(string userName)
        {
            using (var session = NHibernateHelper.OpenSession())
            {
                var list = session.QueryOver<Account>().Where(x => x.UserName == userName).List();
                if (list != null && list.Count > 0)
                {
                    return list[0];
                }
                return null;
            }
        }

        /// <summary>
        /// 添加新账号
        /// </summary>
        public void AddAccount(Account account)
        {
            using (var session = NHibernateHelper.OpenSession())
            {
                using (var transaction = session.BeginTransaction())
                {
                    session.Save(account);
                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// 更新账号信息
        /// </summary>
        public void UpdateAccount(Account account)
        {
            using (var session = NHibernateHelper.OpenSession())
            {
                using (var transaction = session.BeginTransaction())
                {
                    session.Update(account);
                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// 删除账号
        /// </summary>
        public void DeleteAccount(long accountId)
        {
            using (var session = NHibernateHelper.OpenSession())
            {
                using (var transaction = session.BeginTransaction())
                {
                    var account = session.Get<Account>(accountId);
                    if (account != null)
                    {
                        session.Delete(account);
                    }
                    transaction.Commit();
                }
            }
        }
    }
}
