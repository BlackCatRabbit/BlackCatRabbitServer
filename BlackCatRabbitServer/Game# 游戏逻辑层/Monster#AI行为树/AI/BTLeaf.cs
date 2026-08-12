using System;

namespace BlackCatRabbitServer
{
    // ==================== 叶子节点基类 ====================

    public abstract class BTLeaf : BTNode
    {
        protected BTLeaf(string name = "") : base(name) { }
    }

    // ==================== Action ====================

    /// <summary>
    /// 动作节点，支持三种重载：
    /// 1. BTAction(action, name)          — Action&lt;BB&gt;，始终 Success
    /// 2. BTAction(action, name)          — Func&lt;BB, bool&gt;，true=Success, false=Running
    /// 3. BTAction(name, execute)         — Func&lt;BB, float, BTStatus&gt;，完整控制
    /// </summary>
    public class BTAction : BTLeaf
    {
        private readonly Func<BTBlackboard, float, BTStatus> _execute;

        /// <summary>瞬发型：无返回值，始终 Success</summary>
        public BTAction(Action<BTBlackboard> action, string name) : base(name)
            => _execute = (bb, dt) => { action(bb); return BTStatus.Success; };

        /// <summary>单帧判断型：返回 true→Success, false→Running</summary>
        public BTAction(Func<BTBlackboard, bool> action, string name) : base(name)
            => _execute = (bb, dt) => action(bb) ? BTStatus.Success : BTStatus.Running;

        /// <summary>完整控制型：可返回 Success/Failure/Running</summary>
        public BTAction(string name, Func<BTBlackboard, float, BTStatus> execute) : base(name)
            => _execute = execute;

        public override BTStatus Tick(BTBlackboard bb, float deltaTime) => _execute(bb, deltaTime);
    }

    // ==================== InstantAction（向后兼容） ====================

    public class BTInstantAction : BTLeaf
    {
        private readonly Action<BTBlackboard> _execute;

        public BTInstantAction(string name, Action<BTBlackboard> execute) : base(name) => _execute = execute;
        public override BTStatus Tick(BTBlackboard bb, float deltaTime) { _execute(bb); return BTStatus.Success; }
    }

    // ==================== Check ====================

    public class BTCheck : BTLeaf
    {
        private readonly Func<BTBlackboard, bool> _check;
        public BTCheck(string name, Func<BTBlackboard, bool> check) : base(name) => _check = check;
        public override BTStatus Tick(BTBlackboard bb, float deltaTime) => _check(bb) ? BTStatus.Success : BTStatus.Failure;
    }

    // ==================== Wait ====================

    public class BTWait : BTLeaf
    {
        private readonly float _duration;
        private float _elapsed;
        public BTWait(string name, float duration) : base(name) => _duration = duration;
        public override void OnEnter(BTBlackboard bb) => _elapsed = 0f;
        public override BTStatus Tick(BTBlackboard bb, float deltaTime)
            => (_elapsed += deltaTime) >= _duration ? BTStatus.Success : BTStatus.Running;
    }
}
