

using System;

//全局ID生成器
namespace BlackCatRabbitServer
{
    public static class IdGenerator
    {
        // ────────── 简单自增ID（ConnectionId、SessionId 等临时ID）──────────
        private static long _nextId = 1;

        /// <summary>
        /// 线程安全地生成递增ID（可用于 ConnectionId、SessionId、PlayerId 等）
        /// </summary>
        public static long Next() => Interlocked.Increment(ref _nextId);

        // ────────── Snowflake 雪花算法（全局唯一的账号ID）──────────
        // 结构: [41位时间戳][10位WorkerId][12位序列号] = 63位正数
        private static readonly DateTime Epoch = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private const int WorkerIdBits = 10;
        private const int SequenceBits = 12;
        private const long MaxWorkerId = (1L << WorkerIdBits) - 1;      // 1023
        private const long MaxSequence   = (1L << SequenceBits) - 1;    // 4095
        private const int TimestampShift = WorkerIdBits + SequenceBits; // 22
        private const int WorkerIdShift  = SequenceBits;                // 12

        private static readonly object _snowLock = new object();
        private static long _workerId = 1;           // 可配置的机器ID
        private static long _lastTimestamp = -1L;
        private static long _sequence = 0L;

        /// <summary>
        /// 设置 Snowflake 的机器ID（多服务器部署时需不同，范围 0~1023）
        /// </summary>
        public static void SetWorkerId(long workerId)
        {
            if (workerId < 0 || workerId > MaxWorkerId)
                throw new ArgumentOutOfRangeException(nameof(workerId), $"WorkerId 必须在 0~{MaxWorkerId} 之间");
            _workerId = workerId;
        }

        /// <summary>
        /// 使用 Snowflake 算法生成全局唯一的账号ID
        /// </summary>
        public static long NextAccountId()
        {
            lock (_snowLock)
            {
                long timestamp = GetCurrentTimestamp();

                // 时钟回拨处理：等待直到追上上次时间戳
                if (timestamp < _lastTimestamp)
                {
                    timestamp = _lastTimestamp + 1;
                }

                if (timestamp == _lastTimestamp)
                {
                    _sequence = (_sequence + 1) & MaxSequence;
                    if (_sequence == 0)
                    {
                        // 当前毫秒序列号用完，等待下一毫秒
                        timestamp = WaitForNextTick(_lastTimestamp);
                    }
                }
                else
                {
                    _sequence = 0;
                }

                _lastTimestamp = timestamp;

                long id = ((timestamp - Epoch.Ticks / TimeSpan.TicksPerMillisecond) << TimestampShift)
                        | (_workerId << WorkerIdShift)
                        | _sequence;

                return id;
            }
        }

        private static long GetCurrentTimestamp()
        {
            return DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond - Epoch.Ticks / TimeSpan.TicksPerMillisecond;
        }

        private static long WaitForNextTick(long lastTimestamp)
        {
            long timestamp = GetCurrentTimestamp();
            while (timestamp <= lastTimestamp)
            {
                System.Threading.Thread.Sleep(0);
                timestamp = GetCurrentTimestamp();
            }
            return timestamp;
        }
    }
}
