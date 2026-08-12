using System;
using System.Collections.Generic;

namespace BlackCatRabbitServer
{
    // ==================== 组合节点基类 ====================

    public abstract class BTComposite : BTNode
    {
        protected readonly List<BTNode> _children = new();
        protected int _runningIndex = -1;

        protected BTComposite(string name = "") : base(name) { }

        protected BTComposite(string name, params BTNode[] children) : base(name)
        {
            foreach (var c in children) _children.Add(c);
        }

        public BTComposite Add(BTNode child)
        {
            _children.Add(child);
            return this;
        }

        public override void OnEnter(BTBlackboard bb) { _runningIndex = -1; }

        public override void OnExit(BTBlackboard bb)
        {
            if (_runningIndex >= 0 && _runningIndex < _children.Count)
                _children[_runningIndex].OnExit(bb);
            _runningIndex = -1;
        }
    }

    // ==================== Selector ====================

    public class BTSelector : BTComposite
    {
        public BTSelector(string name = "") : base(name) { }
        public BTSelector(string name, params BTNode[] children) : base(name, children) { }

        public override BTStatus Tick(BTBlackboard bb, float deltaTime)
        {
            int start = _runningIndex >= 0 ? _runningIndex : 0;

            for (int i = start; i < _children.Count; i++)
            {
                var child = _children[i];
                if (i != _runningIndex) child.OnEnter(bb);

                var status = child.Tick(bb, deltaTime);

                if (status == BTStatus.Running) { _runningIndex = i; return BTStatus.Running; }
                if (status == BTStatus.Success) { child.OnExit(bb); _runningIndex = -1; return BTStatus.Success; }

                child.OnExit(bb);
            }

            _runningIndex = -1;
            return BTStatus.Failure;
        }
    }

    // ==================== Sequence ====================

    public class BTSequence : BTComposite
    {
        public BTSequence(string name = "") : base(name) { }
        public BTSequence(string name, params BTNode[] children) : base(name, children) { }

        public override BTStatus Tick(BTBlackboard bb, float deltaTime)
        {
            int start = _runningIndex >= 0 ? _runningIndex : 0;

            for (int i = start; i < _children.Count; i++)
            {
                var child = _children[i];
                if (i != _runningIndex) child.OnEnter(bb);

                var status = child.Tick(bb, deltaTime);

                if (status == BTStatus.Running) { _runningIndex = i; return BTStatus.Running; }
                if (status == BTStatus.Failure) { child.OnExit(bb); _runningIndex = -1; return BTStatus.Failure; }

                child.OnExit(bb);
            }

            _runningIndex = -1;
            return BTStatus.Success;
        }
    }

    // ==================== ReactiveSelector ====================

    public class BTReactiveSelector : BTComposite
    {
        public BTReactiveSelector(string name = "") : base(name) { }
        public BTReactiveSelector(string name, params BTNode[] children) : base(name, children) { }

        public override BTStatus Tick(BTBlackboard bb, float deltaTime)
        {
            for (int i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                if (i != _runningIndex) child.OnEnter(bb);

                var status = child.Tick(bb, deltaTime);

                if (status == BTStatus.Running) { _runningIndex = i; return BTStatus.Running; }

                child.OnExit(bb);

                if (status == BTStatus.Success) { _runningIndex = -1; return BTStatus.Success; }
            }

            _runningIndex = -1;
            return BTStatus.Failure;
        }
    }

    // ==================== RandomSelector ====================

    public class BTRandomSelector : BTComposite
    {
        private int[] _shuffled = Array.Empty<int>();
        private readonly System.Random _rng = new();

        public BTRandomSelector(string name = "") : base(name) { }
        public BTRandomSelector(string name, params BTNode[] children) : base(name, children) { }

        public override BTStatus Tick(BTBlackboard bb, float deltaTime)
        {
            int start = _runningIndex >= 0 ? _runningIndex : 0;

            // 只在首次进入时洗牌，Running 期间保持顺序不变
            if (_runningIndex < 0)
            {
                if (_shuffled.Length != _children.Count)
                    _shuffled = new int[_children.Count];
                for (int i = 0; i < _children.Count; i++) _shuffled[i] = i;
                for (int i = _children.Count - 1; i > 0; i--)
                {
                    int j = _rng.Next(i + 1);
                    (_shuffled[i], _shuffled[j]) = (_shuffled[j], _shuffled[i]);
                }
            }

            for (int si = start; si < _shuffled.Length; si++)
            {
                var child = _children[_shuffled[si]];
                if (si != _runningIndex) child.OnEnter(bb);
                var status = child.Tick(bb, deltaTime);
                if (status == BTStatus.Running) { _runningIndex = si; return BTStatus.Running; }
                if (status == BTStatus.Success) { child.OnExit(bb); _runningIndex = -1; return BTStatus.Success; }
                child.OnExit(bb);
            }
            _runningIndex = -1;
            return BTStatus.Failure;
        }
    }

    // ==================== ReactiveSequence ====================

    public class BTReactiveSequence : BTComposite
    {
        public BTReactiveSequence(string name = "") : base(name) { }
        public BTReactiveSequence(string name, params BTNode[] children) : base(name, children) { }

        public override BTStatus Tick(BTBlackboard bb, float deltaTime)
        {
            for (int i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                if (i != _runningIndex) child.OnEnter(bb);

                var status = child.Tick(bb, deltaTime);

                if (status == BTStatus.Running) { _runningIndex = i; return BTStatus.Running; }

                child.OnExit(bb);

                if (status == BTStatus.Failure) { _runningIndex = -1; return BTStatus.Failure; }
            }

            _runningIndex = -1;
            return BTStatus.Success;
        }
    }
}
