/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */
using System;

namespace TaskSchedulerEngine
{
    /// <summary>
    /// Tracks the retry lifetime for a given invocation 
    /// </summary>
    public class RetryableTaskArgs
    {
        public RetryableTaskArgs(IScheduledTask task, int maxAttempts, int baseRetryIntervalSeconds)
        {
            if (baseRetryIntervalSeconds < 2) throw new ArgumentOutOfRangeException("Minimum retry interval is 2 seconds.");
            if (maxAttempts < 1) throw new ArgumentOutOfRangeException("Minimum maxAttempts is 1.");

            Task = task;
            MaxAttempts = maxAttempts;
            BaseRetryIntervalSeconds = baseRetryIntervalSeconds;
        }
        /// <summary>
        /// Maximum total attempts, including first attempt and n retries. 
        /// </summary>
        public int MaxAttempts { get; set; }
        public int BaseRetryIntervalSeconds { get; set; }
        public int RetryCount { get; set; } = 0;

        /// <summary>
        /// When cloned, this will be a pointer to the original Task object, so it will be reused
        /// </summary>
        public IScheduledTask Task { get; set; }

        public RetryableTaskArgs Clone()
        {
            return (RetryableTaskArgs) this.MemberwiseClone();
        }
    }
}