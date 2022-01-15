/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */
using System;
using System.Linq;
using System.Threading;

namespace TaskSchedulerEngine
{
    public class ScheduleRule
    {
        public ScheduleRule()
        {
        }

        public string? Name { get; set; } = null;

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

        public int[] Months { get; set; } = new int[] { };

        /// <summary>
        /// List of months, where 1=Jan, or Empty for any
        /// </summary>
        public ScheduleRule AtMonths(params int[] value)
        {
            if(value.Where(v => v > 12 || v < 1).Count() > 0)
                throw new ArgumentOutOfRangeException("Month must be between 1 (Jan) and 12 (Dec), inclusive.");
            Months = value;
            return this;
        }

        public int[] DaysOfMonth { get; set; } = new int[] { };
        /// <summary>
        /// 1 to 31 or Empty fo any
        /// </summary>
        public ScheduleRule AtDaysOfMonth(params int[] value)
        {
            if(value.Where(v => v > 31 || v < 1).Count() > 0)
                throw new ArgumentOutOfRangeException("DaysOfMonth must be between 1 and 31, inclusive.");
            
            DaysOfMonth = value;
            return this;
        }

        public int[] DaysOfWeek { get; set; } = new int[] { };
        /// <summary>
        /// 0=Sunday, 1=Mon... 6=Saturday or Empty for any
        /// </summary>
        public ScheduleRule AtDaysOfWeek(params int[] value)
        {
            if(value.Where(v => v > 6 || v < 0).Count() > 0)
                throw new ArgumentOutOfRangeException("DaysOfWeek must be between 0 (Sun) and 6 (Sat), inclusive.");
            
            DaysOfWeek = value;
            return this;
        }
        public int[] Hours { get; set; } = new int[] { };
        /// <summary>
        /// 0 (12am, start of the day) to 23 (11pm)
        /// </summary>
        /// <param name="value"></param>
        public ScheduleRule AtHours(params int[] value)
        {
            if(value.Where(v => v > 23 || v < 0).Count() > 0)
                throw new ArgumentOutOfRangeException("Hours must be between 0 (12am, start of day) and 23 (11pm), inclusive");
            
            Hours = value;
            return this;
        }
        public int[] Minutes { get; set; } = new int[] { };
        /// <summary>
        /// 0 to 59
        /// </summary>
        public ScheduleRule AtMinutes(params int[] value)
        {
            if(value.Where(v => v > 59 || v < 0).Count() > 0)
                throw new ArgumentOutOfRangeException("Minutes must be between 0 and 59, inclusive");
            
            Minutes = value;
            return this;
        }
        public int[] Seconds { get; set; } = new int[] { };
        /// <summary>
        /// 0 to 59
        /// </summary>
        public ScheduleRule AtSeconds(params int[] value)
        {
            if(value.Where(v => v > 59 || v < 0).Count() > 0)
                throw new ArgumentOutOfRangeException("Seconds must be between 0 and 59, inclusive");
            
            Seconds = value;
            return this;
        }

        public DateTimeKind Kind { get; set; } = DateTimeKind.Utc;
        public ScheduleRule WithUtc()
        {
            Kind = DateTimeKind.Utc;
            return this;
        }
        public ScheduleRule WithLocalTime()
        {
            Kind = DateTimeKind.Local;
            return this;
        }

        public DateTime Expiration { get; set; } = DateTime.MaxValue;
        public ScheduleRule ExpiresAfter(DateTime value)
        {
            Expiration = value;
            return this;
        }

        public IScheduledTask? Task { get; set; }

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
