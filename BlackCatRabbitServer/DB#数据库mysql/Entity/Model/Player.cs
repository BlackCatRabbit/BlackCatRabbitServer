using System;
using System.Collections.Generic;
using System.Linq;

namespace BlackCatRabbitServer
{
    /// <summary>
    /// 玩家本体数据（不含角色）
    /// </summary>
    public class Player
    {
        public virtual long PlayerId { get; set; }              // 玩家ID
        public virtual long AccountId { get; set; }             // 所属账号ID
        public virtual string Name { get; set; }                // 玩家名称
        public virtual string OwnedCharacterIds { get; set; }   // 拥有的角色模板列表(逗号分隔,如"10001,10002")
        public virtual int Level { get; set; }
        public virtual long Exp { get; set; }
        public virtual DateTime CreatedAt { get; set; }

        /// <summary> 获取拥有的角色模板ID列表 </summary>
        public virtual List<long> GetOwnedCharacters()
        {
            if (string.IsNullOrEmpty(OwnedCharacterIds))
                return new List<long>();
            return OwnedCharacterIds.Split(',').Select(long.Parse).ToList();
        }

        /// <summary> 设置拥有的角色模板ID列表 </summary>
        public virtual void SetOwnedCharacters(List<long> ids)
        {
            OwnedCharacterIds = string.Join(",", ids);
        }
    }
}
