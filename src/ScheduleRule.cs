/*
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleToAttribute("TaskSchedulerEngineTests")]


namespace TaskSchedulerEngine
{
    public class ScheduleRule
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

        /// <summary>
        /// Cron string in the format minute (0..59), hour (0..23), dayOfMonth (1..31), month (1..12), dayOfWeek (0=Sunday..6).
        /// Always sets seconds to 0; call <see cref="AtSeconds"/> afterward to override.
        /// Supports stars, comma-separated lists, ranges, and steps, for example <c>*/15</c>, <c>1,15,30</c>, or <c>9-17/2</c>.
        /// </summary>
        /// <param name="cronExp">Five-field cron expression: minute hour day-of-month month day-of-week.</param>
        /// <returns>The current schedule rule.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="cronExp"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="cronExp"/> is not a supported five-field cron expression.</exception>
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

            if (cronExp == null)
                throw new ArgumentNullException(nameof(cronExp));

            var cronParts = cronExp.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (cronParts.Length != 5)
            {
                throw new ArgumentException("Cron expression must have 5 parts!");
            }

            this.Seconds = new int[] { 0 };
            this.Minutes = ParseCronField(cronParts[0], upperLimit: 59);
            this.Hours = ParseCronField(cronParts[1], upperLimit: 23);
            this.DaysOfMonth = ParseCronField(cronParts[2], 31, 1);
            this.Months = ParseCronField(cronParts[3], 12, 1);
            this.DaysOfWeek = ParseCronField(cronParts[4], 6);

            if (Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }

        /// <summary>
        /// Parses one cron field into concrete values.
        /// </summary>
        /// <param name="segment">A single cron field, such as <c>*</c>, <c>1,2,3</c>, <c>5-10</c>, or <c>*/15</c>.</param>
        /// <param name="upperLimit">The largest allowed value for the field.</param>
        /// <param name="lowerLimit">The smallest allowed value for the field.</param>
        /// <returns>An empty array for a bare wildcard; otherwise the expanded values for the field.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="segment"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="segment"/> is malformed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A field value is outside the provided bounds.</exception>
        public static int[] ParseCronField(string segment, int upperLimit = 59, int lowerLimit = 0)
        {
            if (segment == null)
                throw new ArgumentNullException(nameof(segment));

            if (lowerLimit > upperLimit)
                throw new ArgumentException("Lower limit cannot be greater than upper limit.", nameof(lowerLimit));

            var field = segment.Trim();
            if (field.Length == 0)
                throw new ArgumentException("Cron segment cannot be empty.", nameof(segment));

            if (field.Any(Char.IsWhiteSpace))
                throw new ArgumentException("Cron segment cannot contain whitespace.", nameof(segment));

            if (field == "*")
                return new int[] { };

            return ExpandCronTerms(field, lowerLimit, upperLimit).ToArray();
        }

        /// <summary>
        /// Expands comma-separated cron terms after the bare wildcard case has been handled.
        /// </summary>
        /// <param name="field">The field text containing one or more comma-separated terms.</param>
        /// <param name="lowerLimit">The smallest allowed value for the field.</param>
        /// <param name="upperLimit">The largest allowed value for the field.</param>
        /// <returns>The expanded values represented by <paramref name="field"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="field"/> contains a malformed term.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A term value or step is outside the allowed bounds.</exception>
        private static IEnumerable<int> ExpandCronTerms(string field, int lowerLimit, int upperLimit)
        {
            foreach (var term in field.Split(','))
            {
                if (term.Length == 0)
                    throw new ArgumentException("Cron segment contains an empty list item.", nameof(field));

                if (term == "*")
                    throw new ArgumentException("A bare wildcard cannot be combined with other cron values.", nameof(field));

                foreach (var value in ParseCronTerm(term, lowerLimit, upperLimit))
                    yield return value;
            }
        }

        /// <summary>
        /// Parses one comma-delimited cron term, including an optional step expression.
        /// </summary>
        /// <param name="term">The term to parse, such as <c>5</c>, <c>5-10</c>, <c>*/15</c>, or <c>5-10/2</c>.</param>
        /// <param name="lowerLimit">The smallest allowed value for the containing cron field.</param>
        /// <param name="upperLimit">The largest allowed value for the containing cron field.</param>
        /// <returns>The expanded values represented by <paramref name="term"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="term"/> is malformed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A value or step is outside the allowed bounds.</exception>
        private static IEnumerable<int> ParseCronTerm(string term, int lowerLimit, int upperLimit)
        {
            var stepParts = term.Split('/');
            if (stepParts.Length > 2 || stepParts[0].Length == 0 || (stepParts.Length == 2 && stepParts[1].Length == 0))
                throw new ArgumentException("Cron step expressions must have values on both sides of '/'.", nameof(term));

            var hasStep = stepParts.Length == 2;
            var step = hasStep
                ? ParseCronInt(stepParts[1], 1, upperLimit - lowerLimit + 1, nameof(term))
                : 1;

            var range = ParseCronBase(stepParts[0], hasStep, lowerLimit, upperLimit);
            return ExpandCronRange(range.Start, range.End, step);
        }

        /// <summary>
        /// Parses a cron item base before step expansion.
        /// </summary>
        /// <param name="text">The base text, such as <c>*</c>, <c>5</c>, or <c>5-10</c>.</param>
        /// <param name="hasStep">Indicates whether the base is followed by a slash step.</param>
        /// <param name="lowerLimit">The smallest allowed value for the containing cron field.</param>
        /// <param name="upperLimit">The largest allowed value for the containing cron field.</param>
        /// <returns>The inclusive start and end values for the range.</returns>
        /// <exception cref="ArgumentException"><paramref name="text"/> is malformed or has a descending range.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A range value is outside the allowed bounds.</exception>
        private static CronRange ParseCronBase(string text, bool hasStep, int lowerLimit, int upperLimit)
        {
            if (text == "*")
                return new CronRange(lowerLimit, upperLimit);

            var rangeParts = text.Split('-');
            if (rangeParts.Length > 2 || rangeParts[0].Length == 0 || (rangeParts.Length == 2 && rangeParts[1].Length == 0))
                throw new ArgumentException("Cron range expressions must have values on both sides of '-'.", nameof(text));

            var start = ParseCronInt(rangeParts[0], lowerLimit, upperLimit, nameof(text));
            var end = rangeParts.Length == 1
                ? hasStep ? upperLimit : start
                : ParseCronInt(rangeParts[1], lowerLimit, upperLimit, nameof(text));

            if (start > end)
                throw new ArgumentException("The start of the range must be less than or equal to the end.", nameof(text));

            return new CronRange(start, end);
        }

        /// <summary>
        /// Parses and bounds-checks one numeric cron value.
        /// </summary>
        /// <param name="value">The numeric text to parse.</param>
        /// <param name="lowerLimit">The smallest accepted value.</param>
        /// <param name="upperLimit">The largest accepted value.</param>
        /// <param name="parameterName">The parameter name to attach to thrown exceptions.</param>
        /// <returns>The parsed integer.</returns>
        /// <exception cref="ArgumentException"><paramref name="value"/> is not a non-negative integer.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is outside the allowed bounds.</exception>
        private static int ParseCronInt(string value, int lowerLimit, int upperLimit, string parameterName)
        {
            if (!value.All(Char.IsDigit) || !Int32.TryParse(value, out var result))
                throw new ArgumentException($"Cron value '{value}' is not a valid integer.", parameterName);

            if (result < lowerLimit || result > upperLimit)
                throw new ArgumentOutOfRangeException(parameterName, $"Cron value must be between {lowerLimit} and {upperLimit}, inclusive.");

            return result;
        }

        /// <summary>
        /// Expands an inclusive integer range using the supplied cron step.
        /// </summary>
        /// <param name="start">The first value in the range.</param>
        /// <param name="end">The last allowed value in the range.</param>
        /// <param name="step">The positive increment between values.</param>
        /// <returns>The stepped values from <paramref name="start"/> through <paramref name="end"/>.</returns>
        private static IEnumerable<int> ExpandCronRange(int start, int end, int step)
        {
            for (var value = start; value <= end; value += step)
                yield return value;
        }

        /// <summary>
        /// Represents the inclusive bounds of an expanded cron field item.
        /// </summary>
        private readonly struct CronRange
        {
            /// <summary>
            /// Creates an inclusive cron range.
            /// </summary>
            /// <param name="start">The first value in the range.</param>
            /// <param name="end">The last value in the range.</param>
            public CronRange(int start, int end)
            {
                Start = start;
                End = end;
            }

            /// <summary>
            /// Gets the first value in the range.
            /// </summary>
            public int Start { get; }

            /// <summary>
            /// Gets the last value in the range.
            /// </summary>
            public int End { get; }
        }

        /// <summary>
        /// Indicates that the rule is active and the runtime will process it.
        /// </summary>
        public bool Active { get; private set; } = true;
        public ScheduleRule AsActive(bool active)
        {
            Active = active;
            if (Runtime != null) Runtime.UpdateSchedule(this);
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
            if (Runtime != null) Runtime.UpdateSchedule(this);
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
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.Where(v => v < MinYear || v > (MinYear + 62)).Count() > 0)
                throw new ArgumentOutOfRangeException($"Year must be between {MinYear} and {MinYear + 62}.");
            Years = value;
            if (Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }

        public int[] Months { get; private set; } = new int[] { };

        /// <summary>
        /// List of months, where 1=Jan, or Empty for any
        /// </summary>
        public ScheduleRule AtMonths(params int[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.Where(v => v > 12 || v < 1).Count() > 0)
                throw new ArgumentOutOfRangeException("Month must be between 1 (Jan) and 12 (Dec), inclusive.");
            Months = value;
            if (Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }

        public int[] DaysOfMonth { get; private set; } = new int[] { };
        /// <summary>
        /// 1 to 31 or Empty fo any
        /// </summary>
        public ScheduleRule AtDaysOfMonth(params int[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.Where(v => v > 31 || v < 1).Count() > 0)
                throw new ArgumentOutOfRangeException("DaysOfMonth must be between 1 and 31, inclusive.");

            DaysOfMonth = value;
            if (Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }

        public int[] DaysOfWeek { get; private set; } = new int[] { };
        /// <summary>
        /// 0=Sunday, 1=Mon... 6=Saturday or Empty for any
        /// </summary>
        public ScheduleRule AtDaysOfWeek(params int[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.Where(v => v > 6 || v < 0).Count() > 0)
                throw new ArgumentOutOfRangeException("DaysOfWeek must be between 0 (Sun) and 6 (Sat), inclusive.");

            DaysOfWeek = value;
            if (Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }
        public int[] Hours { get; private set; } = new int[] { };
        /// <summary>
        /// 0 (12am, start of the day) to 23 (11pm)
        /// </summary>
        /// <param name="value"></param>
        public ScheduleRule AtHours(params int[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.Where(v => v > 23 || v < 0).Count() > 0)
                throw new ArgumentOutOfRangeException("Hours must be between 0 (12am, start of day) and 23 (11pm), inclusive");

            Hours = value;
            if (Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }
        public int[] Minutes { get; private set; } = new int[] { };
        /// <summary>
        /// 0 to 59
        /// </summary>
        public ScheduleRule AtMinutes(params int[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.Where(v => v > 59 || v < 0).Count() > 0)
                throw new ArgumentOutOfRangeException("Minutes must be between 0 and 59, inclusive");

            Minutes = value;
            if (Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }
        public int[] Seconds { get; private set; } = new int[] { };
        /// <summary>
        /// 0 to 59
        /// </summary>
        public ScheduleRule AtSeconds(params int[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.Where(v => v > 59 || v < 0).Count() > 0)
                throw new ArgumentOutOfRangeException("Seconds must be between 0 and 59, inclusive");

            Seconds = value;
            if (Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }

        public TimeZoneInfo TimeZone { get; private set; } = TimeZoneInfo.Utc;
        public ScheduleRule WithUtc()
        {
            TimeZone = TimeZoneInfo.Utc;
            if (Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }
        public ScheduleRule WithLocalTime()
        {
            TimeZone = TimeZoneInfo.Local;
            if (Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }
        /// <summary>
        /// Looks up time zone from the system.
        /// </summary>
        /// <param name="tzId">Rules defined here https://learn.microsoft.com/en-us/dotnet/api/system.timezoneinfo.findsystemtimezonebyid</param>
        public ScheduleRule WithTimeZone(string tzId)
        {
            if (tzId == null)
                throw new ArgumentNullException(nameof(tzId));

            try
            {
                return WithTimeZone(TimeZoneInfo.FindSystemTimeZoneById(tzId));
            }
            catch (TimeZoneNotFoundException)
            {
                throw new ArgumentException($"System cannot find time zone {tzId}.", nameof(tzId));
            }
        }
        public ScheduleRule WithTimeZone(TimeZoneInfo tz)
        {
            TimeZone = tz ?? throw new ArgumentNullException(nameof(tz));
            if (Runtime != null) Runtime.UpdateSchedule(this);
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
                .AtHours(time.Hour)
                .AtMinutes(time.Minute)
                .AtSeconds(time.Second)
                .WithUtc()
                .ExpiresAfter(time.AddMinutes(1));
            if (Runtime != null) Runtime.UpdateSchedule(this);
            return s;
        }



        /// <summary>
        /// Execute once for each year specified, on first second of first minute of first hour of first day of first month of year
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public ScheduleRule ExecuteEveryYear(params int[] value)
        {
            return this.AtYears(value)
                .AtMonths(1)
                .AtDaysOfMonth(1)
                .AtHours(0)
                .AtMinutes(0)
                .AtSeconds(0);
        }

        /// <summary>
        /// Execute once for each month specified, on first second of first minute of first hour of first day month
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public ScheduleRule ExecuteEveryMonth(params int[] value)
        {
            return this
                .AtMonths(value)
                .AtDaysOfMonth(1)
                .AtHours(0)
                .AtMinutes(0)
                .AtSeconds(0);
        }


        /// <summary>
        /// Execute once for day of month specified, on first second of first minute of day
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public ScheduleRule ExecuteEveryDayOfMonth(params int[] value)
        {
            return this
                .AtDaysOfMonth(value)
                .AtHours(0)
                .AtMinutes(0)
                .AtSeconds(0);
        }

        /// <summary>
        /// Execute once for each day of week specified, on first second of first minute of first hour of day
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public ScheduleRule ExecuteEveryDayOfWeek(params int[] value)
        {
            return this.AtDaysOfWeek(value)
                .AtHours(0)
                .AtMinutes(0)
                .AtSeconds(0);
        }

        /// <summary>
        /// Execute once for each hour specified, on first second of first minute of hour
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public ScheduleRule ExecuteEveryHour(params int[] value)
        {
            return this.AtHours(value)
                .AtMinutes(0)
                .AtSeconds(0);
        }

        /// <summary>
        /// Execute once for each minute specified, on first second of minute
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public ScheduleRule ExecuteEveryMinute(params int[] value)
        {
            return this.AtMinutes(value)
                .AtSeconds(0);
        }

        /// <summary>
        /// Execute once for each second specified
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public ScheduleRule ExecuteEverySecond(params int[] value)
        {
            return this.AtSeconds(value ?? Enumerable.Range(0, 60).ToArray());
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
            if (Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }

        public IScheduledTask? Task { get; set; }

        /// <summary>
        /// Execute a task with exponential backoff algorithm. See ExponentialBackoffTask for details.
        /// </summary>
        public ScheduleRule ExecuteAndRetry(Func<ScheduleRuleMatchEventArgs, CancellationToken, Task<bool>> callback, int maxAttempts, int baseRetryIntervalSeconds)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            Task = new ExponentialBackoffTask(callback, maxAttempts, baseRetryIntervalSeconds);
            if (Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }

        /// <summary>
        /// Execute a task with exponential backoff algorithm. See ExponentialBackoffTask for details.
        /// </summary>
        public ScheduleRule ExecuteAndRetry(Func<ScheduleRuleMatchEventArgs, CancellationToken, bool> callback, int maxAttempts, int baseRetryIntervalSeconds)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            return ExecuteAndRetry(async (e, ct) => callback(e, ct), maxAttempts, baseRetryIntervalSeconds);
        }

        public ScheduleRule Execute(Func<ScheduleRuleMatchEventArgs, CancellationToken, Task<bool>> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            Task = new AnonymousScheduledTask(callback);
            if (Runtime != null) Runtime.UpdateSchedule(this);
            return this;
        }

        public ScheduleRule Execute(Func<ScheduleRuleMatchEventArgs, CancellationToken, bool> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            return Execute(async (e, ct) => callback(e, ct));
        }

        public ScheduleRule Execute(IScheduledTask taskInstance)
        {
            Task = taskInstance ?? throw new ArgumentNullException(nameof(taskInstance));
            if (Runtime != null) Runtime.UpdateSchedule(this);
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
            if (val == null) return "* (null)";
            if (val.Length == 0) return "* (empty)";

            string m = "";
            foreach (var i in val)
            {
                m = m + i.ToString() + ",";
            }
            return m.TrimEnd(',');
        }

    }
}
