/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Configuration;

namespace TaskSchedulerEngine
{
    public class Schedule
    {
        public Schedule()
        {
        }

        public Schedule(string name)
        {
            _name = name;
        }

        public string Name
        {
            get
            {
                if (!string.IsNullOrEmpty(_name))
                {
                    return _name;
                }
                else
                {
                    return Guid.NewGuid().ToString();
                }
            }
            set
            {
                _name = value;
            }
        }
        string _name;


        /// <summary>
        /// Specify the name/unique identifier of the schedule
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public Schedule WithName(string name)
        {
            Name = name;
            return this;
        }

        public int[] Months { get { return _months; } }
        int[] _months;
        /// <summary>
        /// List of months, where 1=Jan, or null for any
        /// </summary>
        public Schedule AtMonths(params int[] value)
        {
            _months = value;
            return this;
        }

        public int[] DaysOfMonth { get { return _daysOfMonth; } }
        int[] _daysOfMonth;
        /// <summary>
        /// 1 to 31
        /// </summary>
        public Schedule AtDaysOfMonth(params int[] value)
        {
            _daysOfMonth = value;
            return this;
        }

        public int[] DaysOfWeek { get { return _daysOfWeek; } }
        int[] _daysOfWeek;
        /// <summary>
        /// 0=Sunday, 1=Mon... 6=Saturday
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public Schedule AtDaysOfWeek(params int[] value)
        {
            _daysOfWeek = value;
            return this;
        }
        public int[] Hours { get { return _hours; } }
        int[] _hours;
        /// <summary>
        /// 0 (12am, start of the day) to 23 (11pm)
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public Schedule AtHours(params int[] value)
        {
            _hours = value;
            return this;
        }
        public int[] Minutes { get { return _minutes; } }
        int[] _minutes;
        /// <summary>
        /// 0 to 59
        /// </summary>
        public Schedule AtMinutes(params int[] value)
        {
            _minutes = value;
            return this;
        }
        public int[] Seconds { get { return _seconds; } }
        int[] _seconds;
        /// <summary>
        /// 0 to 59
        /// </summary>
        public Schedule AtSeconds(params int[] value)
        {
            _seconds = value;
            return this;
        }
        public DateTimeKind Kind { get { return _kind; } }
        DateTimeKind _kind = DateTimeKind.Utc;
        public Schedule WithUtc()
        {
            _kind = DateTimeKind.Utc;
            return this;
        }
        public Schedule WithLocalTime()
        {
            _kind = DateTimeKind.Local;
            return this;
        }

        public List<KeyValuePair<Type, object>> Tasks { get { return _tasks; } }
        List<KeyValuePair<Type, object>> _tasks = new List<KeyValuePair<Type, object>>();
        public Schedule Execute<T>(object parameters) where T : ITask
        {
            var value = new KeyValuePair<Type, object>(
                typeof(T),
                parameters);
            _tasks.Add(value);
            return this;
        }
        public Schedule Execute<T>() where T : ITask
        {
            return Execute<T>(null);
        }

    }
}
