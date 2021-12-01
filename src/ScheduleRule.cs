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
using System.Threading.Tasks;
using System.Threading;

namespace TaskSchedulerEngine
{
    public class ScheduleRule
    {
        public ScheduleRule()
        {
        }

        public ScheduleRule(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }

        /// <summary>
        /// Specify the name/unique identifier of the schedule
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public ScheduleRule WithName(string name)
        {
            Name = name;
            return this;
        }

        public int[] Months { get; private set; }

        /// <summary>
        /// List of months, where 1=Jan, or null for any
        /// </summary>
        public ScheduleRule AtMonths(params int[] value)
        {
            Months = value;
            return this;
        }

        public int[] DaysOfMonth { get; private set; }
        /// <summary>
        /// 1 to 31
        /// </summary>
        public ScheduleRule AtDaysOfMonth(params int[] value)
        {
            DaysOfMonth = value;
            return this;
        }

        public int[] DaysOfWeek { get; private set; }
        /// <summary>
        /// 0=Sunday, 1=Mon... 6=Saturday
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public ScheduleRule AtDaysOfWeek(params int[] value)
        {
            DaysOfWeek = value;
            return this;
        }
        public int[] Hours { get; private set; }
        /// <summary>
        /// 0 (12am, start of the day) to 23 (11pm)
        /// </summary>
        /// <param name="value"></param>
        public ScheduleRule AtHours(params int[] value)
        {
            Hours = value;
            return this;
        }
        public int[] Minutes { get; private set; }
        /// <summary>
        /// 0 to 59
        /// </summary>
        public ScheduleRule AtMinutes(params int[] value)
        {
            Minutes = value;
            return this;
        }
        public int[] Seconds { get; private set; }
        /// <summary>
        /// 0 to 59
        /// </summary>
        public ScheduleRule AtSeconds(params int[] value)
        {
            Seconds = value;
            return this;
        }
        
        public DateTimeKind Kind { get { return _kind; } }
        DateTimeKind _kind = DateTimeKind.Utc;
        public ScheduleRule WithUtc()
        {
            _kind = DateTimeKind.Utc;
            return this;
        }
        public ScheduleRule WithLocalTime()
        {
            _kind = DateTimeKind.Local;
            return this;
        }

        public IScheduledTask Task { get; set; }

        public ScheduleRule Execute(Action<ScheduleRuleMatchEventArgs, CancellationToken> callback)
        {
            Task = new AnonymousScheduledTask(callback);
            return this;
        }

        public ScheduleRule Execute(IScheduledTask taskInstance)
        {
            Task = taskInstance;
            return this;
        }

    }
}
