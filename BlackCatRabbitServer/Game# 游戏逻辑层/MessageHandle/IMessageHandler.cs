using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackCatRabbitServer
{ 
// 定义泛型接口
    public interface IMessageHandler<T> where T : class
    {
        Task Handle(Session session, T message);
    }
}
