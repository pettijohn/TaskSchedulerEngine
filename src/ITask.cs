/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace TaskSchedulerEngine
{
    public interface ITask
    {
        /// <summary>
        /// Executes the task. Execution information is provided.
        /// </summary>
        void OnScheduleRuleMatch(ScheduleRuleMatchEventArgs e, CancellationToken c);
    }
}
