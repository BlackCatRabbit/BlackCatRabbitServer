using FluentNHibernate.Mapping;

namespace BlackCatRabbitServer
{
    public class AccountMap : ClassMap<Account>
    {
        public AccountMap()
        {
            Table("accounts");
            Id(x => x.AccountId).Column("account_id").GeneratedBy.Assigned();
            Map(x => x.UserName).Column("username").Not.Nullable().Unique().Length(64);
            Map(x => x.Password).Column("password").Not.Nullable().Length(128);
            Map(x => x.CreatedAt).Column("created_at").Default("CURRENT_TIMESTAMP");
        }
    }
}
