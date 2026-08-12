using System;
using System.Collections.Generic;

namespace BlackCatRabbitServer
{
    public class PlayersDBManager
    {
        /// <summary> 根据玩家ID获取玩家 </summary>
        public Player GetPlayerById(long playerId)
        {
            using (var session = NHibernateHelper.OpenSession())
            {
                return session.Get<Player>(playerId);
            }
        }

        /// <summary> 根据玩家名获取玩家 </summary>
        public Player GetPlayerByName(string name)
        {
            using (var session = NHibernateHelper.OpenSession())
            {
                var list = session.QueryOver<Player>().Where(x => x.Name == name).List();
                return (list != null && list.Count > 0) ? list[0] : null;
            }
        }

        /// <summary> 根据账号ID获取该账号下的所有玩家 </summary>
        public IList<Player> GetPlayersByAccountId(long accountId)
        {
            using (var session = NHibernateHelper.OpenSession())
            {
                return session.QueryOver<Player>().Where(x => x.AccountId == accountId).List();
            }
        }

        /// <summary> 添加新玩家 </summary>
        public void AddPlayer(Player player)
        {
            using (var session = NHibernateHelper.OpenSession())
            {
                using (var transaction = session.BeginTransaction())
                {
                    session.Save(player);
                    transaction.Commit();
                }
            }
        }

        /// <summary> 更新玩家信息 </summary>
        public void UpdatePlayer(Player player)
        {
            using (var session = NHibernateHelper.OpenSession())
            {
                using (var transaction = session.BeginTransaction())
                {
                    session.Update(player);
                    transaction.Commit();
                }
            }
        }

        /// <summary> 删除玩家 </summary>
        public void DeletePlayer(long playerId)
        {
            using (var session = NHibernateHelper.OpenSession())
            {
                using (var transaction = session.BeginTransaction())
                {
                    var player = session.Get<Player>(playerId);
                    if (player != null)
                        session.Delete(player);
                    transaction.Commit();
                }
            }
        }
    }
}
