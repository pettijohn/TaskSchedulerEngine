/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */
using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskSchedulerEngine
{
    /// <summary>
    /// A collection of bit fields for a schedule definition. This allows fast bitwise arithmatic 
    /// evaluation of the schedule against Now.
    /// </summary>
    public class ScheduleEvaluationOptimized
    {
        public ScheduleEvaluationOptimized(ScheduleRule sched)
        {
            if(sched.Task == null)
                throw new ArgumentNullException("ScheduleRule must have a Task, set it with Execute()");

            this.Name = sched.Name;
            this.Year = ParseIntArrayToBitfield(sched.Years.Select(y => y - ScheduleRule.MinYear));
            this.Month = ParseIntArrayToBitfield(sched.Months);
            this.DayOfMonth = ParseIntArrayToBitfield(sched.DaysOfMonth);
            this.DayOfWeek = ParseIntArrayToBitfield(sched.DaysOfWeek);
            this.Hour = ParseIntArrayToBitfield(sched.Hours);
            this.Minute = ParseIntArrayToBitfield(sched.Minutes);
            this.Second = ParseIntArrayToBitfield(sched.Seconds);
            this.Kind = sched.Kind;
            this.Task = sched.Task;
        }

        private const string PARSE_BOUNDS_ERROR = "The only allowable values for scheduling are from 0 to 62.";

        public IScheduledTask Task { get; private set; }

        /// <summary>
        /// Convert an int[] such as {0,15,47} to a bitfield with the 0th, 15th and 47th bits set to 1.
        /// </summary>
        public static long ParseIntArrayToBitfield(IEnumerable<int> value)
        {
            //Undefined or wildcard
            if (value == null || value.Count() == 0)
            {
                return -1;
            }
            else
            {
                long destinationField = 0;
                foreach (int nthBit in value)
                {
                    if (nthBit > 62 || nthBit < 0)
                    {
                        throw new ArgumentOutOfRangeException(PARSE_BOUNDS_ERROR);
                    }
                    //1L << n is mathematically the same as 2^^n
                    destinationField |= (1L << nthBit);
                }
                return destinationField;
            }
        }

        /// <summary>
        /// Convert a string such as "0,15,47" to an array of integers {0,15,47}
        /// </summary>
        public static IEnumerable<int> ParseStringToIntArray(string value)
        {
            //Undefined or wildcard
            if (String.IsNullOrEmpty(value) || value == "*")
            {
                yield break;
            }
            else
            {
                string[] values = value.Split(new char[] { ',' });
                //If the number is parsed as zero, it becomes 2^^0 => 1.
                //If the number parsed is one, it becomes 2^^1 => 2.
                foreach (string numberValue in values)
                {
                    int nthBit;
                    if (Int32.TryParse(numberValue.Trim(), out nthBit))
                    {
                        yield return nthBit;
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException(PARSE_BOUNDS_ERROR);
                    }
                }
            }
        }

        /// <summary>
        /// Convert a string such as "0,15,47" to a bitfield with the 0th, 15th and 47th bits set to 1.
        /// </summary>
        public static long ParseStringToBitfield(string value)
        {
            return ParseIntArrayToBitfield(ParseStringToIntArray(value));
        }

        /// <summary>
        /// Primary key of the schedule. 
        /// </summary>
        public string? Name { get; set; }
        /// <summary>
        /// Bitfield represents years since ScheduleRule.MinYear
        /// </summary>
        public long Year { get; set; }
        public long Month { get; set; }
        public long DayOfMonth { get; set; }
        public long DayOfWeek { get; set; }
        public long Hour { get; set; }
        public long Minute { get; set; }
        public long Second { get; set; }
        public DateTimeKind Kind { get; set; }

        /// <summary>
        /// Compare the provided DateTime to the schedule definition. If the event should occur,
        /// this will return a <see cref="ScheduleRuleMatchEventArgs"/> for passing to the callback, 
        /// else it will return null.
        /// </summary>
        /// <param name="DateTime">In UTC</param>
        /// <returns></returns>
        public bool EvaluateRuleMatch(DateTime inputValueUtc)
        {
            //The inputValue is in UTC, but the rule supports comparing in Local time.
            //Determine which we want to compare and save it as compareValue.
            DateTime compareValue = Kind == DateTimeKind.Local ? compareValue = inputValueUtc.ToLocalTime() : compareValue = inputValueUtc;
            
            if(compareValue.Year - ScheduleRule.MinYear < 0) throw new OverflowException("Error evaluating Year paramater in the past.");

            //Perform a bitwise AND on the compareValue and this. If the result is non-zero, then there is a match.
            //1 << x is the same as 2^^x, just faster since it's not a floating point op.
            return (((1L << (compareValue.Year - ScheduleRule.MinYear) & this.Year) != 0)
                && ((1L << compareValue.Month & this.Month) != 0)
                && ((1L << compareValue.Day & this.DayOfMonth) != 0)
                && ((1L << (int)compareValue.DayOfWeek & this.DayOfWeek) != 0)
                && ((1L << compareValue.Hour & this.Hour) != 0)
                && ((1L << compareValue.Minute & this.Minute) != 0)
                && ((1L << compareValue.Second & this.Second) != 0));
        }

    }
}
