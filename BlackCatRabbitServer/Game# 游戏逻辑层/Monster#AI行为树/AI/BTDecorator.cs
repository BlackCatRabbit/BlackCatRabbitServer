using System;

namespace BlackCatRabbitServer
{
    // ==================== 装饰节点基类 ====================

    public abstract class BTDecorator : BTNode
    {
        protected readonly BTNode _child;

        protected BTDecorator(string name, BTNode child) : base(name) { _child = child; }
        public override void OnEnter(BTBlackboard bb) => _child?.OnEnter(bb);
        public override void OnExit(BTBlackboard bb) => _child?.OnExit(bb);
    }

    // ==================== Condition：条件节点 ====================

    /// <summary>
    /// 两种形态：
    /// 装饰器模式：条件通过 → 执行子节点，失败 → 返回 Failure
    /// 叶子模式 ：纯条件检查，通过 → Success，失败 → Failure
    /// </summary>
    public class BTCondition : BTDecorator
    {
        private readonly Func<BTBlackboard, bool> _check;
        private readonly bool _isLeaf;

        // 装饰器：BTCondition("IsDead", check, child)
        public BTCondition(string name, Func<BTBlackboard, bool> check, BTNode child)
            : base(name, child) { _check = check; _isLeaf = false; }

        // 叶子：BTCondition(check, "IsDead")
        public BTCondition(Func<BTBlackboard, bool> check, string name)
            : base(name, null) { _check = check; _isLeaf = true; }

        public override BTStatus Tick(BTBlackboard bb, float deltaTime)
        {
            if (!_check(bb)) return BTStatus.Failure;
            return _isLeaf ? BTStatus.Success : _child.Tick(bb, deltaTime);
        }
    }

    // ==================== Inverter ====================

    public class BTInverter : BTDecorator
    {
        public BTInverter(string name, BTNode child) : base(name, child) { }

        public override BTStatus Tick(BTBlackboard bb, float deltaTime)
        {
            var result = _child.Tick(bb, deltaTime);
            return result switch
            {
                BTStatus.Success => BTStatus.Failure,
                BTStatus.Failure => BTStatus.Success,
                _ => BTStatus.Running,
            };
        }
    }
}
