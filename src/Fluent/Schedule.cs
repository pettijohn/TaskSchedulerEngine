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

        int[] _months;
        /// <summary>
        /// List of months, where 1=Jan, or null for any
        /// </summary>
        public Schedule AtMonths(params int[] value)
        {
            _months = value;
            return this;
        }

        int[] _daysOfMonth;
        /// <summary>
        /// 1 to 31
        /// </summary>
        public Schedule AtDaysOfMonth(params int[] value)
        {
            _daysOfMonth = value;
            return this;
        }

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
        int[] _minutes;
        /// <summary>
        /// 0 to 59
        /// </summary>
        public Schedule AtMinutes(params int[] value)
        {
            _minutes = value;
            return this;
        }
        int[] _seconds;
        /// <summary>
        /// 0 to 59
        /// </summary>
        public Schedule AtSeconds(params int[] value)
        {
            _seconds = value;
            return this;
        }
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

        List<KeyValuePair<Type, String>> _tasks = new List<KeyValuePair<Type, string>>();
        public Schedule Execute<T>(string parameters) where T : ITask
        {
            var m = MethodInfo.GetCurrentMethod();
            var value = new KeyValuePair<Type, string>(
                m.GetGenericArguments().First(),
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
