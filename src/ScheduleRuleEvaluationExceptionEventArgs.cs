/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */
using System;

namespace TaskSchedulerEngine
{
    /// <summary>
    /// Diagnostic information reported when a schedule rule cannot be evaluated.
    /// </summary>
    public class ScheduleRuleEvaluationExceptionEventArgs : EventArgs
    {
        public ScheduleRuleEvaluationExceptionEventArgs(
            ScheduleRule scheduleRule,
            DateTimeOffset timeEvaluatedUtc,
            Exception exception)
        {
            ScheduleRule = scheduleRule ?? throw new ArgumentNullException(nameof(scheduleRule));
            TimeEvaluatedUtc = timeEvaluatedUtc;
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        /// <summary>
        /// The schedule rule that failed evaluation.
        /// </summary>
        public ScheduleRule ScheduleRule { get; private set; }

        /// <summary>
        /// The UTC second being evaluated when the failure occurred.
        /// </summary>
        public DateTimeOffset TimeEvaluatedUtc { get; private set; }

        /// <summary>
        /// The exception thrown while evaluating the schedule rule.
        /// </summary>
        public Exception Exception { get; private set; }
    }
}
