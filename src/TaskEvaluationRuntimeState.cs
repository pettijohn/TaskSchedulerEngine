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
    internal enum TaskEvaluationRuntimeState
    {
        /// <summary>
        /// Not running, okay to start.
        /// </summary>
        Stopped,
        /// <summary>
        /// Stop the pump background thread and initiate graceful shutdown.
        /// </summary>
        StopRequested,
        /// <summary>
        /// Waiting for executing tasks to finish gracefully.
        /// </summary>
        StoppingGracefully,
        /// <summary>
        /// Tasks are evaluating every second.
        /// </summary>
        Running
    }
}
