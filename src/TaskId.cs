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
    /// <summary>
    /// Keep a singleton task ID and increment it.
    /// </summary>
    internal static class TaskId
    {
        private static object _criticalSectionLock = new object();
        private static int _currentTaskId = 0;

        /// <summary>
        /// Returns the current task ID without incrementing it.
        /// </summary>
        /// <returns></returns>
        public static int PeekCurrent()
        {
            lock(_criticalSectionLock)
            {
                return _currentTaskId;
            }
        }

        /// <summary>
        /// Increment the task ID and return the new value.
        /// </summary>
        /// <returns></returns>
        public static int Increment()
        {
            lock(_criticalSectionLock);
            {
                _currentTaskId++;
                return _currentTaskId;
            }
        }

    }
}
