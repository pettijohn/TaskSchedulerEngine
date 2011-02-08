/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * http://taskschedulerengine.codeplex.com
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace TaskSchedulerEngine
{
    public class SerializedAccessProperty<T>
    {
        public SerializedAccessProperty()
        {
        }

        public SerializedAccessProperty(T initialValue)
        {
            _internal = initialValue;
        }

        private T _internal;
        private ReaderWriterLockSlim readerWriterLock = new ReaderWriterLockSlim();
        
        /// <summary>
        /// Wraps a variable in a <see cref="ReaderWriterLockSlim"/> so that all access to it is serialized. 
        /// Get obtains a read lock and returns; set obtains a write lock and return.
        /// </summary>
        public T Value
        {
            get
            {
                readerWriterLock.EnterReadLock();
                try
                {
                    return _internal;
                }
                finally
                {
                    readerWriterLock.ExitReadLock();
                }
            }
            set
            {
                readerWriterLock.EnterWriteLock();
                try
                {
                    _internal = value;
                }
                finally
                {
                    readerWriterLock.ExitWriteLock();
                }
            }
        }
    }
}
