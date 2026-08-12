using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using NHibernate;
using NHibernate.Tool.hbm2ddl;

namespace BlackCatRabbitServer
{
    public class NHibernateHelper
    {
        private static ISessionFactory _sessionFactory = null;//单例模式

        private static void InitializeSessionFactory()
        {
            //连接数据库,没有的表自动建立
            _sessionFactory =
                Fluently.Configure().Database(MySQLConfiguration.Standard.ConnectionString(db => db.Server("127.0.0.1").Database("blackcatrabbitgameserver").Username("root").Password("root")))
                    .Mappings(x => x.FluentMappings.AddFromAssemblyOf<NHibernateHelper>())
                    .ExposeConfiguration(cfg => new SchemaUpdate(cfg).Execute(false, true))
                    .BuildSessionFactory();
        }

        private static ISessionFactory SessionFactory
        {
            get
            {
                if (_sessionFactory == null)
                    InitializeSessionFactory();
                return _sessionFactory;
            }
        }

        public static ISession OpenSession()
        {
            return SessionFactory.OpenSession();
        }
    }
}
