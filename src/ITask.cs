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
        void Tick(object sender, TickEventArgs e);
        
        /// <summary>
        /// Called after the constructor.
        /// </summary>
        void Initialize(BitwiseSchedule schedule, object parameters);
    }
}
