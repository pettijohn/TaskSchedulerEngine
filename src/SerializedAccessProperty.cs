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
    public class SerializedAccessProperty<T>
    {
        public SerializedAccessProperty()
        {
            _readerWriterLock = new ReaderWriterLockSlim();
        }

        public SerializedAccessProperty(T initialValue) : this()
        {
            _internal = initialValue;
        }

        ~SerializedAccessProperty()
        {
            if (_readerWriterLock != null) 
                _readerWriterLock.Dispose();
        }

        private T _internal;
        private ReaderWriterLockSlim _readerWriterLock;

        /// <summary>
        /// Wraps a variable in a <see cref="ReaderWriterLockSlim"/> so that all access to it is serialized. 
        /// Get obtains a read lock and returns; set obtains a write lock and return.
        /// </summary>
        public T Value
        {
            get
            {
                _readerWriterLock.EnterReadLock();
                try
                {
                    return _internal;
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }
            }
            set
            {
                _readerWriterLock.EnterWriteLock();
                try
                {
                    _internal = value;
                }
                finally
                {
                    _readerWriterLock.ExitWriteLock();
                }
            }
        }
    }
}
