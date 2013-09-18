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
    /// <summary>
    /// Enumeration to track the run state of the task evaluation pump.
    /// </summary>
    internal enum TaskPumpRunState
    {
        /// <summary>
        /// Not running. Set internally.
        /// </summary>
        Stopped = 0,
        /// <summary>
        /// Use to request that the background thread stop. The background 
        /// thread will change to <see cref="Stopped"/> when done shutting down
        /// </summary>
        Stopping,
        /// <summary>
        /// Tasks are evaluating as normal.
        /// </summary>
        Running
    }
}
