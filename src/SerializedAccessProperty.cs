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
        }

        public SerializedAccessProperty(T initialValue)
        {
            _internal = initialValue;
        }

        private T _internal;
        private object _lockObject = new object();
        
        /// <summary>
        /// Wraps a variable in a <see cref="ReaderWriterLockSlim"/> so that all access to it is serialized. 
        /// Get obtains a read lock and returns; set obtains a write lock and return.
        /// </summary>
        public T Value
        {
            get
            {
                lock(_lockObject)
                {
                    return _internal;
                }
            }
            set
            {
                lock(_lockObject)
                {
                    _internal = value;
                }
            }
        }
    }
}
