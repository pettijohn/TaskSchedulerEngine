/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TaskSchedulerEngine
{
    public interface ITask
    {
        /// <summary>
        /// Executes the task. Execution information is provided.
        /// </summary>
        void HandleConditionsMetEvent(object sender, ConditionsMetEventArgs e);
        
        /// <summary>
        /// Called after the constructor.
        /// </summary>
        void Initialize(ScheduleDefinition schedule, object parameters);
    }
}
