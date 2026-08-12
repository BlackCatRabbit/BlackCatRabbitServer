using System;

namespace BlackCatRabbitServer
{
    /// <summary>
    /// 角色数据（1个Player可以有多个Character）
    /// </summary>
    public class Character
    {
        public virtual long CharacterId { get; set; }       // 角色实例ID（唯一，Character表主键）
        public virtual long PlayerId { get; set; }          // 所属玩家ID
        public virtual long CharacterTemplateId { get; set; } // 角色模板Id
        public virtual int SkinId { get; set; }             // 皮肤ID
        public virtual int LastSceneId { get; set; }        // 上次所在场景ID
        public virtual float PosX { get; set; }             // 角色位置X
        public virtual float PosY { get; set; }             // 角色位置Y
        public virtual float PosZ { get; set; }             // 角色位置Z
        public virtual float RotX { get; set; }             // 角色旋转角度X
        public virtual float RotY { get; set; }             // 角色旋转角度Y
        public virtual float RotZ { get; set; }             // 角色旋转角度Z
        public virtual DateTime CreatedAt { get; set; }
    }
}