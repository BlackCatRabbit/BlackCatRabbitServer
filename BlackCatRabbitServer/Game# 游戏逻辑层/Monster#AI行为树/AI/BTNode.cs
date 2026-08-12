using System;
using System.Collections.Generic;

namespace BlackCatRabbitServer
{
    // ==================== 节点状态 ====================

    public enum BTStatus
    {
        Success,
        Failure,
        Running,
    }

    // ==================== 黑板 ====================

    /// <summary>行为树节点间共享数据的黑板</summary>
    public class BTBlackboard
    {
        private readonly Dictionary<string, object> _data = new();

        public void Set(string key, object value) => _data[key] = value;
        public T Get<T>(string key)
        {
            if (_data.TryGetValue(key, out var v) && v is T t)
                return t;
            return default;
        }
        public bool TryGet<T>(string key, out T value)
        {
            if (_data.TryGetValue(key, out var v) && v is T t)
            {
                value = t;
                return true;
            }
            value = default;
            return false;
        }
        public bool Has(string key) => _data.ContainsKey(key);
        public void Remove(string key) => _data.Remove(key);
        public void Clear() => _data.Clear();
    }

    // ==================== 基类 ====================

    /// <summary>行为树节点基类</summary>
    public abstract class BTNode
    {
        public string Name;
        protected BTNode(string name = "") { Name = string.IsNullOrEmpty(name) ? GetType().Name : name; }
        public abstract BTStatus Tick(BTBlackboard bb, float deltaTime);

        /// <summary>节点被激活时调用（Running → 重新进入）</summary>
        public virtual void OnEnter(BTBlackboard bb) { }

        /// <summary>节点被中断或完成时调用</summary>
        public virtual void OnExit(BTBlackboard bb) { }
    }
}
