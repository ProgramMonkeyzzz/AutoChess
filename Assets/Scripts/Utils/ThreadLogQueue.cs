using System.Collections.Concurrent;

namespace AutoChess.Utils
{
    /// <summary>战斗工作线程向 Unity 主线程转发的日志级别。</summary>
    public enum ThreadLogLevel
    {
        Info,
        Warning,
        Error
    }

    /// <summary>一条待由 Unity 主线程输出的线程日志。</summary>
    public readonly struct ThreadLogRecord
    {
        /// <summary>日志级别。</summary>
        public readonly ThreadLogLevel Level;

        /// <summary>日志文本。</summary>
        public readonly string Message;

        /// <summary>创建线程日志记录。</summary>
        public ThreadLogRecord(ThreadLogLevel level, string message)
        {
            Level = level;
            Message = message;
        }
    }

    /// <summary>线程安全的日志缓冲区，工作线程只入队，主线程负责输出。</summary>
    public static class ThreadLogQueue
    {
        private static readonly ConcurrentQueue<ThreadLogRecord> Records =
            new ConcurrentQueue<ThreadLogRecord>();

        /// <summary>写入一条待处理日志。</summary>
        public static void Enqueue(ThreadLogLevel level, string message)
        {
            Records.Enqueue(new ThreadLogRecord(level, message));
        }

        /// <summary>尝试取出一条待处理日志。</summary>
        public static bool TryDequeue(out ThreadLogRecord record)
        {
            return Records.TryDequeue(out record);
        }
    }
}
