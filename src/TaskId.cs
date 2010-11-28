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
        private static ReaderWriterLockSlim _criticalSectionLock = new ReaderWriterLockSlim();
        private static int _currentTaskId = 0;

        /// <summary>
        /// Returns the current task ID without incrementing it.
        /// </summary>
        /// <returns></returns>
        public static int PeekCurrent()
        {
            _criticalSectionLock.EnterReadLock();
            try
            {
                return _currentTaskId;
            }
            finally
            {
                _criticalSectionLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Increment the task ID and return the new value.
        /// </summary>
        /// <returns></returns>
        public static int Increment()
        {
            _criticalSectionLock.EnterWriteLock();
            try
            {
                _currentTaskId++;
                return _currentTaskId;
            }
            finally
            {
                _criticalSectionLock.ExitWriteLock();
            }
        }

    }
}
