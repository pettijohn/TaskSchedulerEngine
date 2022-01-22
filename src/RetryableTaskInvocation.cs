using System;
using System.Threading;

namespace TaskSchedulerEngine
{
    /// <summary>
    /// When a retryable task starts, this handles the n retry lifecycle. 
    /// </summary>
    internal class RetryableTaskInvocation : IScheduledTask
    {
        public RetryableTaskInvocation(RetryableTaskArgs args)
        {
            _args = args;
        }

        private RetryableTaskArgs _args { get; set; }
        private object _lockObject = new Object();
        

        public bool OnScheduleRuleMatch(ScheduleRuleMatchEventArgs e, CancellationToken c)
        {
            // 0th invocation 
            bool success = _args.Task.OnScheduleRuleMatch(e, c);
            lock (_lockObject) //thread safety 
            {
                // Compare against MaxAttempts - 1. 
                // E.g. If MaxAttempts is 1, we will invoke the 0th above and then test to add a retry. 
                // If MaxAttempts is 2, we will already have invoked 0th and 1st.
                // Increment RetryCount after adding to scheduler so that first retry attempt happens after
                // BaseRetryIntervalSeconds * (2^0) == BaseRetryIntervalSeconds. 
                if (!success && _args.RetryCount < _args.MaxAttempts-1)
                {
                    //Re evaluate this task in BaseRetryIntervalSeconds*(2^RetryCount) seconds
                    e.Runtime.AddSchedule(new ScheduleRule()
                        .ExecuteOnce(DateTime.UtcNow.AddSeconds(_args.BaseRetryIntervalSeconds * (Math.Pow(2, _args.RetryCount))))
                        .Execute(this));
                    _args.RetryCount++;
                }
            }

            return success;
        }
    }
}