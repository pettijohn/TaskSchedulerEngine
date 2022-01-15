/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */
using System.Threading;

namespace TaskSchedulerEngine
{
    public interface IScheduledTask
    {
        /// <summary>
        /// Executes the task. Execution information is provided.
        /// </summary>
        void OnScheduleRuleMatch(ScheduleRuleMatchEventArgs e, CancellationToken c);
    }
}
