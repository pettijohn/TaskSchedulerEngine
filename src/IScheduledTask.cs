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
        /// Executes the task. 
        /// </summary>
        /// <param name="e">Information about the scheduled time of task.</param>
        /// <param name="c">Allow graceful shutdown.</param>
        /// <returns>Optional success value; useful for e.g. retry scenarios.</returns>
        bool OnScheduleRuleMatch(ScheduleRuleMatchEventArgs e, CancellationToken c);
    }
}
