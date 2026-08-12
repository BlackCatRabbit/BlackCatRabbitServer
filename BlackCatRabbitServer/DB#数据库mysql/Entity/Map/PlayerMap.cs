using FluentNHibernate.Mapping;

namespace BlackCatRabbitServer
{
    public class PlayerMap : ClassMap<Player>
    {
        public PlayerMap()
        {
            Table("players");
            Id(x => x.PlayerId).Column("player_id").GeneratedBy.Assigned();
            Map(x => x.AccountId).Column("account_id").Not.Nullable();
            Map(x => x.Name).Column("name").Not.Nullable().Unique().Length(64);
            Map(x => x.OwnedCharacterIds).Column("owned_character_ids").Length(1024).Default("");
            Map(x => x.Level).Column("level").Default("1");
            Map(x => x.Exp).Column("exp").Default("0");
            Map(x => x.CreatedAt).Column("created_at").Default("CURRENT_TIMESTAMP");
        }
    }
}
