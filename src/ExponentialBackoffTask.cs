using System;
using System.Threading;

namespace TaskSchedulerEngine
{
    /// <summary>
    /// Exponential backoff retry of a task. Retry interval is baseRetrySeconds*(2^retryCount).
    /// Passes through to execute IScheduledTask; if it fails, this task adds itself back to the scheduler runtime. 
    /// This does not handle or swallow exceptions - IScheduledTask must gracefully fail return false from OnScheduleRuleMatch.
    /// </summary>
    public class ExponentialBackoffTask : IScheduledTask
    {
        public ExponentialBackoffTask(Func<ScheduleRuleMatchEventArgs, CancellationToken, bool> callback, int maxAttempts, int baseRetryInteravalSeconds)
            : this(new RetryableTaskArgs(new AnonymousScheduledTask(callback), maxAttempts, baseRetryInteravalSeconds))
        {
        }
        public ExponentialBackoffTask(RetryableTaskArgs args)
        {
            _args = args;
        }

        private RetryableTaskArgs _args;

        public bool OnScheduleRuleMatch(ScheduleRuleMatchEventArgs e, CancellationToken c)
        {
            //Factory pattern needs to create a new instance to manage the retry lifetime of this invocation 
            // If we didn't clone then future instances here would start from already having their retries "used up."
            var retryInvocation = new RetryableTaskInvocation(_args.Clone());
            return retryInvocation.OnScheduleRuleMatch(e, c);
        }
    }
}