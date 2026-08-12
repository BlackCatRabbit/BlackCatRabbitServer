using FluentNHibernate.Mapping;

namespace BlackCatRabbitServer
{
    public class CharacterMap : ClassMap<Character>
    {
        public CharacterMap()
        {
            Table("characters");
            Id(x => x.CharacterId).Column("character_id").GeneratedBy.Assigned();
            Map(x => x.PlayerId).Column("player_id").Not.Nullable();
            Map(x => x.CharacterTemplateId).Column("character_template_id").Default("0");
            Map(x => x.SkinId).Column("skin_id").Default("1");
            Map(x => x.LastSceneId).Column("last_scene_id").Default("1");
            Map(x => x.PosX).Column("pos_x").Default("0");
            Map(x => x.PosY).Column("pos_y").Default("0");
            Map(x => x.PosZ).Column("pos_z").Default("0");
            Map(x => x.RotX).Column("rot_x").Default("0");
            Map(x => x.RotY).Column("rot_y").Default("0");
            Map(x => x.RotZ).Column("rot_z").Default("0");
            Map(x => x.CreatedAt).Column("created_at").Default("CURRENT_TIMESTAMP");
        }
    }
}