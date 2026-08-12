using System;

namespace BlackCatRabbitServer
{
    public class GameLoop
    {
        private Timer _timer;
        private bool _running;
        private float _totalTime;

        public void Start()
        {
            _running = true;
            _totalTime = 0;

            // 每 100ms 执行一次（10Hz）
            _timer = new Timer(_ =>
            {
                if (!_running) return;
                _totalTime += 0.1f;

                // 投递到逻辑线程执行，确保线程安全
                JobQueue.Instance.Enqueue(() =>
                {
                    // 怪物AI驱动 + 状态广播
                    MonsterManager.Instance.Tick(0.1f);


                });
            }, null, 0, 100);
        }

        public void Stop()
        {
            _running = false;
            _timer?.Dispose();
        }
    }
}
