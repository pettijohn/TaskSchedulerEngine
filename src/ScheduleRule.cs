/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleToAttribute("TaskSchedulerEngineTests")]


namespace TaskSchedulerEngine
{
    public partial class ScheduleRule
    {
        internal ScheduleRule()
        {
            // Only used for unit testing 
        }
        internal ScheduleRule(TaskEvaluationRuntime runtime)
        {
            Runtime = runtime;
        }

        protected TaskEvaluationRuntime? Runtime;

        // Co-authored by Bing/Chat GPT (but it had bugs that I fixed!)
        internal const string CronRegex = @"^\s*((\*|[0-5]?\d)(,[0-5]?\d)*)\s+((\*|[01]?\d|2[0-3])(,([01]?\d|2[0-3]))*)\s+((\*|0?[0-9]|[12][0-9]|3[01])(,(0?[0-9]|[12][0-9]|3[01]))*)\s+((\*|0?[1-9]|1[012])(,(0?[1-9]|1[012]))*)\s+((\*|[0-6])(,[0-6])*)\s*$";
        
#if NET7_0_OR_GREATER
        [GeneratedRegex(CronRegex, RegexOptions.IgnoreCase, "en-US")]
        private static partial Regex CronCompiledRegex();
#endif 

        /// <summary>
        /// Cron string in the format minute (0..59), hour (0..23), dayOfMonth (1..31), month (1..12), dayOfWeek (0=Sunday..6).
        /// Will always set Seconds=0; call .AtSeconds() to override. Supports comma separated lists of numbers or stars. 
        /// DOES NOT support step (/) or range (-) operators. 
        /// </summary>
        public ScheduleRule FromCron(string cronExp)
        {
            /*

            # Example of job definition:
            # .---------------- minute (0 - 59)
            # |  .------------- hour (0 - 23)
            # |  |  .---------- day of month (1 - 31)
            # |  |  |  .------- month (1 - 12) 
            # |  |  |  |  .---- day of week (0 - 6) (Sunday=0) 
            # |  |  |  |  |
            # *  *  *  *  *

            */

#if NET7_0_OR_GREATER
            var match = CronCompiledRegex().Match(cronExp);
#else
            var match = Regex.Match(cronExp, CronRegex); 
#endif
            if(!match.Success) throw new ArgumentException("Unable to parse cron expression. Only comma-separated digits and * are accepted. Digits must conform to their meaning (e.g. minute must be between 0-59, etc)", "cronExp");

            // groups 1 4 8 12 16
            this.Seconds     = new int[] { 0 };
            this.Minutes     = match.Groups[1].Value == "*" ? new int[] { } : match.Groups[1].Value.Split(",").Select(s => Int32.Parse(s)).ToArray();
            this.Hours       = match.Groups[4].Value == "*" ? new int[] { } : match.Groups[4].Value.Split(",").Select(s => Int32.Parse(s)).ToArray();
            this.DaysOfMonth = match.Groups[8].Value == "*" ? new int[] { } : match.Groups[8].Value.Split(",").Select(s => Int32.Parse(s)).ToArray();
            this.Months      = match.Groups[12].Value == "*" ? new int[] { } : match.Groups[12].Value.Split(",").Select(s => Int32.Parse(s)).ToArray();
            this.DaysOfWeek  = match.Groups[16].Value == "*" ? new int[] { } : match.Groups[16].Value.Split(",").Select(s => Int32.Parse(s)).ToArray();

            if(Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        public bool Active { get; private set; } = true;
        public ScheduleRule AsActive(bool active)
        {
            Active = active;
            if(Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }

        public string? Name { get; private set; } = null;

        /// <summary>
        /// Specify the name/unique identifier of the schedule
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public ScheduleRule WithName(string name)
        {
            Name = name;
            if(Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }

        /// <summary>
        /// Set the floor of the year to last year. Not using this year because there's probably some edge case
        /// crossing the year boundary between local time and UTC. Not hard coding a value so that it will increment
        /// without being recompiled forever. 
        /// </summary>
        internal static readonly int MinYear = DateTimeOffset.Now.Year - 1;
        public int[] Years { get; private set; } = new int[] { };

        public ScheduleRule AtYears(params int[] value)
        {
            if(value.Where(v => v < MinYear || v > (MinYear+62)).Count() > 0)
                throw new ArgumentOutOfRangeException($"Year must be between {MinYear} and {MinYear+62}.");
            Years = value;
            if(Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }

        public int[] Months { get; private set; } = new int[] { };

        /// <summary>
        /// List of months, where 1=Jan, or Empty for any
        /// </summary>
        public ScheduleRule AtMonths(params int[] value)
        {
            if(value.Where(v => v > 12 || v < 1).Count() > 0)
                throw new ArgumentOutOfRangeException("Month must be between 1 (Jan) and 12 (Dec), inclusive.");
            Months = value;
            if(Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }

        public int[] DaysOfMonth { get; private set; } = new int[] { };
        /// <summary>
        /// 1 to 31 or Empty fo any
        /// </summary>
        public ScheduleRule AtDaysOfMonth(params int[] value)
        {
            if(value.Where(v => v > 31 || v < 1).Count() > 0)
                throw new ArgumentOutOfRangeException("DaysOfMonth must be between 1 and 31, inclusive.");
            
            DaysOfMonth = value;
            if(Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }

        public int[] DaysOfWeek { get; private set; } = new int[] { };
        /// <summary>
        /// 0=Sunday, 1=Mon... 6=Saturday or Empty for any
        /// </summary>
        public ScheduleRule AtDaysOfWeek(params int[] value)
        {
            if(value.Where(v => v > 6 || v < 0).Count() > 0)
                throw new ArgumentOutOfRangeException("DaysOfWeek must be between 0 (Sun) and 6 (Sat), inclusive.");
            
            DaysOfWeek = value;
            if(Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }
        public int[] Hours { get; private set; } = new int[] { };
        /// <summary>
        /// 0 (12am, start of the day) to 23 (11pm)
        /// </summary>
        /// <param name="value"></param>
        public ScheduleRule AtHours(params int[] value)
        {
            if(value.Where(v => v > 23 || v < 0).Count() > 0)
                throw new ArgumentOutOfRangeException("Hours must be between 0 (12am, start of day) and 23 (11pm), inclusive");
            
            Hours = value;
            if(Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }
        public int[] Minutes { get; private set; } = new int[] { };
        /// <summary>
        /// 0 to 59
        /// </summary>
        public ScheduleRule AtMinutes(params int[] value)
        {
            if(value.Where(v => v > 59 || v < 0).Count() > 0)
                throw new ArgumentOutOfRangeException("Minutes must be between 0 and 59, inclusive");
            
            Minutes = value;
            if(Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }
        public int[] Seconds { get; private set; } = new int[] { };
        /// <summary>
        /// 0 to 59
        /// </summary>
        public ScheduleRule AtSeconds(params int[] value)
        {
            if(value.Where(v => v > 59 || v < 0).Count() > 0)
                throw new ArgumentOutOfRangeException("Seconds must be between 0 and 59, inclusive");
            
            Seconds = value;
            if(Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }

        public TimeZoneInfo TimeZone { get; private set; } = TimeZoneInfo.Utc;
        public ScheduleRule WithUtc()
        {
            TimeZone = TimeZoneInfo.Utc;
            if(Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }
        public ScheduleRule WithLocalTime()
        {
            TimeZone = TimeZoneInfo.Local;
            if(Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }
        /// <summary>
        /// Looks up time zone from the system.
        /// </summary>
        /// <param name="tzId">Rules defined here https://learn.microsoft.com/en-us/dotnet/api/system.timezoneinfo.findsystemtimezonebyid</param>
        public ScheduleRule WithTimeZone(string tzId)
        {
            try {
                return WithTimeZone(TimeZoneInfo.FindSystemTimeZoneById(tzId));
            }
            catch (TimeZoneNotFoundException) {
                throw new ArgumentException($"System cannot find time zone {tzId}.", nameof(tzId));
            }
        }
        public ScheduleRule WithTimeZone(TimeZoneInfo tz)
        {
            TimeZone = tz;
            if(Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }

        /// <summary>
        /// Execute once at the floor of the second specified. Shorthand for setting Year, Month, DayOfMonth, Second, and Expiration.
        /// </summary>
        public ScheduleRule ExecuteOnceAt(DateTimeOffset time)
        {
            time = time.ToUniversalTime();
            var s = this.AtYears(time.Year)
                .AtMonths(time.Month)
                .AtDaysOfMonth(time.Day)
                .AtSeconds(time.Second)
                .WithUtc()
                .ExpiresAfter(time.AddMinutes(1));
            if(Runtime != null) Runtime.UpdateSchedule(this);
            return s;
        }

        public DateTimeOffset Expiration { get; private set; } = DateTimeOffset.MaxValue;
        /// <summary>
        /// Time after which the task shall be deleted from the scheduler. 
        /// If not deleted, ongoing added schedules (e.g. in retry scenario) will live forever
        /// and memory leak. 
        /// </summary>
        public ScheduleRule ExpiresAfter(DateTimeOffset value)
        {
            Expiration = value;
            if(Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }

        public IScheduledTask? Task { get; set; }

        /// <summary>
        /// Execute a task with exponential backoff algorithm. See ExponentialBackoffTask for details. 
        /// </summary>
        public ScheduleRule ExecuteAndRetry(Func<ScheduleRuleMatchEventArgs, CancellationToken, Task<bool>> callback, int maxAttempts, int baseRetryIntervalSeconds)
        {
            Task = new ExponentialBackoffTask(callback, maxAttempts, baseRetryIntervalSeconds);
            if(Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }

        /// <summary>
        /// Execute a task with exponential backoff algorithm. See ExponentialBackoffTask for details. 
        /// </summary>
        public ScheduleRule ExecuteAndRetry(Func<ScheduleRuleMatchEventArgs, CancellationToken, bool> callback, int maxAttempts, int baseRetryIntervalSeconds)
        {
            return ExecuteAndRetry(async (e, ct) => callback(e, ct), maxAttempts, baseRetryIntervalSeconds);
        }

        public ScheduleRule Execute(Func<ScheduleRuleMatchEventArgs, CancellationToken, Task<bool>> callback)
        {
            Task = new AnonymousScheduledTask(callback);
            if(Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }

        public ScheduleRule Execute(Func<ScheduleRuleMatchEventArgs, CancellationToken, bool> callback)
        {
            return Execute(async (e, ct) => callback(e, ct));
        }

        public ScheduleRule Execute(IScheduledTask taskInstance)
        {
            Task = taskInstance;
            if(Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }

        /// <summary>
        /// Print debugging info about this schedule
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return @$"Schedule has name {Name}. Rule:
Seconds: {PrintArr(Seconds)}
Minutes: {PrintArr(Minutes)}
Hours: {PrintArr(Hours)}
DaysOfWeek: {PrintArr(DaysOfWeek)}
Months: {PrintArr(Months)}
DaysOfMonth: {PrintArr(DaysOfMonth)}
Years: {PrintArr(Years)}
TimeZone: {TimeZone.DisplayName}
Expires: {Expiration}
            ";
        }

        private string PrintArr(int[] val)
        {
            if(val == null) return "* (null)";
            if(val.Length == 0) return "* (empty)";
            
            string m = "";
            foreach (var i in val)
            {
                m = m + i.ToString() + ",";
            }
            return m.TrimEnd(',');
        }

    }
}
