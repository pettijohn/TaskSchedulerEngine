using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaskSchedulerEngine.Configuration;
using System.Reflection;

namespace TaskSchedulerEngine.Fluent
{
    public class Schedule
    {
        public Schedule()
        {
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

        public List<KeyValuePair<Type, String>> Tasks { get { return _tasks; } }
        List<KeyValuePair<Type, String>> _tasks = new List<KeyValuePair<Type, string>>();
        public Schedule Execute<T>(string parameters) where T : ITask
        {
            var value = new KeyValuePair<Type, string>(
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
