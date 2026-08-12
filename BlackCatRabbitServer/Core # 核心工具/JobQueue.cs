using System.Collections.Concurrent;
//单线程逻辑队列
namespace BlackCatRabbitServer
{
    public class JobQueue
    {
        private static JobQueue _instance;
        private static readonly object _lockObj = new object();

        private readonly ConcurrentQueue<Action> _jobs = new();
        private readonly AutoResetEvent _waitHandle = new(false);
        private bool _running = true;

        public static JobQueue Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObj)
                    {
                        if (_instance == null)
                        {
                            _instance = new JobQueue();
                        }
                    }
                }
                return _instance;
            }
        }

        public void Enqueue(Action job)
        {
            _jobs.Enqueue(job);
            _waitHandle.Set();
        }

        public void Start()
        {
            Task.Run(() =>
            {
                while (_running)
                {
                    _waitHandle.WaitOne();
                    while (_jobs.TryDequeue(out var job))//直接处理队列为空
                    {
                        try { job(); }
                        catch (Exception e) { Console.WriteLine($"[Job异常] {e}"); }
                    }
                }
            });
        }

        public void Stop() { _running = false; _waitHandle.Set(); }
    }
}
