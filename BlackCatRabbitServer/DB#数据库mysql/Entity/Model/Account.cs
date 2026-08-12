using System;

namespace BlackCatRabbitServer
{
    public class Account
    {
        public virtual long AccountId { get; set; }
        public virtual string UserName { get; set; }
        public virtual string Password { get; set; }
        public virtual DateTime CreatedAt { get; set; }
    }
}
