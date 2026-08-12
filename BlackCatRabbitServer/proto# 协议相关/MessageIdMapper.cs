using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackCatRabbitServer
{
    // 放在静态类中
    public static class MessageIdMapper
    {
        private static readonly Dictionary<string, int> NameToValue = new Dictionary<string, int>();

        // 静态构造函数，程序启动时执行一次
        static MessageIdMapper()
        {
            foreach (int value in Enum.GetValues(typeof(MessageId)))
            {
                string name = Enum.GetName(typeof(MessageId), value);
                NameToValue[name] = value; // 缓存起来
            }
        }

        public static int GetValue(string name)
        {
            // 找不到返回 -1 或抛出异常，由上层决定
            return NameToValue.TryGetValue(name, out int value) ? value : -1;
        }
}


    }
