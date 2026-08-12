using System.Collections.Generic;

namespace BlackCatRabbitServer
{
    public class CharacterDBManager
    {
        /// <summary> 根据角色ID获取角色 </summary>
        public Character GetCharacterById(long characterId)
        {
            using (var session = NHibernateHelper.OpenSession())
            {
                return session.Get<Character>(characterId);
            }
        }

        /// <summary> 根据玩家ID获取该玩家下的所有角色 </summary>
        public IList<Character> GetCharactersByPlayerId(long playerId)
        {
            using (var session = NHibernateHelper.OpenSession())
            {
                return session.QueryOver<Character>().Where(x => x.PlayerId == playerId).List();
            }
        }

        /// <summary> 添加角色 </summary>
        public void AddCharacter(Character character)
        {
            using (var session = NHibernateHelper.OpenSession())
            {
                using (var transaction = session.BeginTransaction())
                {
                    session.Save(character);
                    transaction.Commit();
                }
            }
        }

        /// <summary> 更新角色 </summary>
        public void UpdateCharacter(Character character)
        {
            using (var session = NHibernateHelper.OpenSession())
            {
                using (var transaction = session.BeginTransaction())
                {
                    session.Update(character);
                    transaction.Commit();
                }
            }
        }

        /// <summary> 删除角色 </summary>
        public void DeleteCharacter(long characterId)
        {
            using (var session = NHibernateHelper.OpenSession())
            {
                using (var transaction = session.BeginTransaction())
                {
                    var character = session.Get<Character>(characterId);
                    if (character != null)
                        session.Delete(character);
                    transaction.Commit();
                }
            }
        }
    }
}