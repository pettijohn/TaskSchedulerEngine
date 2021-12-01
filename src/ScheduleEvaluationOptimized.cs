﻿/* 
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
    /// <summary>
    /// A collection of bit fields for a schedule definition. This allows fast bitwise arithmatic 
    /// evaluation of the schedule against Now.
    /// </summary>
    public class ScheduleEvaluationOptimized
    {
        public ScheduleEvaluationOptimized(ScheduleRule sched)
        {
            this._originalSchedule = sched;
            this.Name = sched.Name;
            this.Month = ParseIntArrayToBitfield(sched.Months);
            this.DayOfMonth = ParseIntArrayToBitfield(sched.DaysOfMonth);
            this.DayOfWeek = ParseIntArrayToBitfield(sched.DaysOfWeek);
            this.Hour = ParseIntArrayToBitfield(sched.Hours);
            this.Minute = ParseIntArrayToBitfield(sched.Minutes);
            this.Second = ParseIntArrayToBitfield(sched.Seconds);
            this.Kind = sched.Kind;
            this.Task = sched.Task;
        }

        private ScheduleRule _originalSchedule;

        private const string PARSE_BOUNDS_ERROR = "The only acceptable values for scheduling are from 0 to 63, an empty string, or the character '*'.";

        public IScheduledTask Task { get; private set; }

        /// <summary>
        /// Keep a counter of how many Tasks have executed. Each Task invocation will have a unique sequential ID.
        /// Only update with System.Threading.Interlocked.Increment(). 
        /// </summary>
        private static long TaskID = 0;

        /// <summary>
        /// Convert an int[] such as {0,15,47} to a bitfield with the 0th, 15th and 47th bits set to 1.
        /// </summary>
        public static long ParseIntArrayToBitfield(IEnumerable<int> value)
        {
            //Undefined or wildcard
            if (value == null)
            {
                return -1;
            }
            else
            {
                long destinationField = 0;
                bool hasElements = false;
                foreach (int nthBit in value)
                {
                    if (nthBit > 62 || nthBit < 0)
                    {
                        throw new ArgumentOutOfRangeException(PARSE_BOUNDS_ERROR);
                    }
                    hasElements = true;
                    //1L << n is mathematically the same as 2^^n
                    destinationField |= (1L << nthBit);
                }
                return hasElements ? destinationField : -1;
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
        public string Name { get; set; }
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
        public ScheduleRuleMatchEventArgs? Evaluate(DateTime inputValueUtc)
        {
            //TODO : move compareValue into a more compare-friendly object. Peformance-wise, probably not ever necessary. 

            //The inputValue is in UTC, but the rule supports comparing in Local time.
            //Determine which we want to compare and save it as compareValue.
            DateTime compareValue = Kind == DateTimeKind.Local ? compareValue = inputValueUtc.ToLocalTime() : compareValue = inputValueUtc;
            
            //Perform a bitwise AND on the compareValue and this. If the result is non-zero, then there is a match.
            //1 << x is the same as 2^^x, just faster since it's not a floating point op.
            bool match = ((1 << compareValue.Month & this.Month) != 0)
                && ((1L << compareValue.Day & this.DayOfMonth) != 0)
                && ((1L << (int)compareValue.DayOfWeek & this.DayOfWeek) != 0)
                && ((1L << compareValue.Hour & this.Hour) != 0)
                && ((1L << compareValue.Minute & this.Minute) != 0)
                && ((1L << compareValue.Second & this.Second) != 0);

            if (match)
            {
                ScheduleRuleMatchEventArgs e = new ScheduleRuleMatchEventArgs();
                e.TimeScheduledUtc = inputValueUtc;
                e.TimeSignaledUtc = DateTime.UtcNow;
                e.TaskId = Interlocked.Increment(ref TaskID);
                e.ScheduleRule = this._originalSchedule;
                return e;
            }

            return null;
        }

    }
}
