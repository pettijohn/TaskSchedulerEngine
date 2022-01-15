/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */
using System;

namespace TaskSchedulerEngine
{
    public class ScheduleRuleMatchEventArgs : EventArgs
    {
        public ScheduleRuleMatchEventArgs(
            DateTime timeSignaledUtc, DateTime timeScheduledUtc, long taskId, 
            ScheduleRule scheduleRule, TaskEvaluationRuntime? runtime
        )
        {
            TimeSignaledUtc = timeSignaledUtc;
            TimeScheduledUtc = timeScheduledUtc;
            TaskId = taskId;
            ScheduleRule = scheduleRule;
            Runtime = runtime;
        }
        public DateTime TimeSignaledUtc { get; private set; }
        public DateTime TimeScheduledUtc { get; private set; }
        public long TaskId { get; set; }
        public ScheduleRule ScheduleRule { get; private set; }
        public TaskEvaluationRuntime? Runtime { get; internal set; }
    }
}
