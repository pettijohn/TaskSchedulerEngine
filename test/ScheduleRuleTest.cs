using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaskSchedulerEngine;
using static SchedulerEngineRuntimeTests.TestAssert;

namespace SchedulerEngineRuntimeTests
{
    /// <summary>
    /// Test both human and machine optimized schedule rules. 
    /// </summary>
    [TestClass]
    public class ScheduleRuleTest
    {
        public ScheduleRuleTest()
        { }

        [TestMethod]
        public void ParseIntArrayToBitfieldTest()
        {
            var testInput = new int[] { 0, 3 };
            long expected = 0b00001001;

            Assert.AreEqual(expected, ScheduleEvaluationOptimized.ParseIntArrayToBitfield(testInput));

            testInput = new int[] { 0, 3, 62 };
            expected = 0b100000000000000000000000000000000000000000000000000000000001001;
            Assert.AreEqual(expected, ScheduleEvaluationOptimized.ParseIntArrayToBitfield(testInput));
        }

        [TestMethod]
        public void BitfieldOverflowException()
        {
            Throws<ArgumentOutOfRangeException>(() =>
                ScheduleEvaluationOptimized.ParseIntArrayToBitfield(new int[] { 63 }));
        }

        [TestMethod]
        public void ParseStringToIntArray()
        {
            var testInput = "0,15,47";
            var expected = new int[] { 0, 15, 47 };

            CollectionAssert.AreEquivalent(expected, ScheduleEvaluationOptimized.ParseStringToIntArray(testInput).ToArray());
        }

        [TestMethod]
        public void EvaluateTest()
        {
            var rule = new ScheduleRule()
                .AtHours(0, 23)
                .AtMinutes(0)
                .AtSeconds(0)
                .AtDaysOfWeek((int)new DateTime(DateTime.Now.Year, 11, 30).DayOfWeek)
                .AtDaysOfMonth(30)
                .AtMonths(11)
                .AtYears(DateTime.Now.Year)
                .WithName("Optional name/ID parameter")
                .WithUtc()
                .Execute((e, c) => { return true; }); //noop callback, as callback cannot be null

            var evalOptimized = new ScheduleEvaluationOptimized(rule);

            var evalTime = new DateTimeOffset(DateTime.Now.Year, 11, 30, 23, 0, 0, TimeSpan.Zero);

            var testResult = evalOptimized.EvaluateRuleMatch(evalTime);
            Assert.IsTrue(testResult);
        }

        [TestMethod]
        public void ExecuteOnceAtTest()
        {
            var executeTime = new DateTimeOffset(DateTime.Now.Year, 11, 30, 23, 0, 0, TimeSpan.Zero);
            var rule = new ScheduleRule()
                .ExecuteOnceAt(executeTime)
                .WithName("Optional name/ID parameter")
                .WithUtc()
                .Execute((e, c) => { return true; }); //noop callback, as callback cannot be null

            var evalOptimized = new ScheduleEvaluationOptimized(rule);

            var evalTime = executeTime;

            var testResult = evalOptimized.EvaluateRuleMatch(evalTime);
            Assert.IsTrue(testResult);

            testResult = evalOptimized.EvaluateRuleMatch(evalTime.AddSeconds(1));
            Assert.IsFalse(testResult);
            testResult = evalOptimized.EvaluateRuleMatch(evalTime.AddSeconds(-1));
            Assert.IsFalse(testResult);
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddMinutes(1)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddHours(1)));
            Assert.AreEqual(executeTime.AddMinutes(1), rule.Expiration);
            testResult = evalOptimized.EvaluateRuleMatch(evalTime.AddYears(1));
            Assert.IsFalse(testResult);
        }

        [TestMethod]
        public void ExecuteEveryYearsTest()
        {
            int baseYear = ScheduleRule.MinYear + 1;
            var evalOptimized = new ScheduleEvaluationOptimized(new ScheduleRule()
                .ExecuteEveryYear(baseYear, baseYear + 1)
                .WithUtc()
                .Execute((e, c) => { return true; }));

            var evalTime = new DateTimeOffset(baseYear, 1, 1, 0, 0, 0, TimeSpan.Zero);
            Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime));
            Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime.AddYears(1)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddYears(2)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddMonths(1)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddDays(1)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddHours(1)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddMinutes(1)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddSeconds(1)));

            evalOptimized = new ScheduleEvaluationOptimized(new ScheduleRule()
                .ExecuteEveryYear()
                .WithUtc()
                .Execute((e, c) => { return true; }));
            for(int i=0; i<10;i++)
                Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime.AddYears(i)));
        }

        [TestMethod]
        public void ExecuteEveryMonthsTest()
        {
            var evalOptimized = new ScheduleEvaluationOptimized(new ScheduleRule()
                .ExecuteEveryMonth(1, 2)
                .WithUtc()
                .Execute((e, c) => { return true; }));
            var evalTime = new DateTimeOffset(ScheduleRule.MinYear + 1, 1, 1, 0, 0, 0, TimeSpan.Zero);
            Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime));
            Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime.AddMonths(1)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddMonths(2)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddDays(1)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddHours(1)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddMinutes(1)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddSeconds(1)));

            evalOptimized = new ScheduleEvaluationOptimized(new ScheduleRule()
                .ExecuteEveryMonth()
                .WithUtc()
                .Execute((e, c) => { return true; }));
            for (int i = 0; i < 12; i++)
                Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime.AddMonths(i)));
        }


        [TestMethod]
        public void ExecuteEveryDaysOfMonthTest()
        {
            var evalOptimized = new ScheduleEvaluationOptimized(new ScheduleRule()
                .ExecuteEveryDayOfMonth(1, 2)
                .WithUtc()
                .Execute((e, c) => { return true; }));
            var evalTime = new DateTimeOffset(ScheduleRule.MinYear + 1, 1, 1, 0, 0, 0, TimeSpan.Zero);
            Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime));
            Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime.AddDays(1)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddDays(2)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddHours(1)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddMinutes(1)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddSeconds(1)));

            evalOptimized = new ScheduleEvaluationOptimized(new ScheduleRule()
                .ExecuteEveryDayOfMonth()
                .WithUtc()
                .Execute((e, c) => { return true; }));
            for(int i=0; i<28;i++)
                Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime.AddDays(i)));
        }

        [TestMethod]
        public void ExecuteEveryDaysOfWeekTest()
        {
            var evalTime = new DateTimeOffset(ScheduleRule.MinYear + 1, 1, 1, 0, 0, 0, TimeSpan.Zero);
            int firstDay = (int)evalTime.DayOfWeek;
            int secondDay = (int)evalTime.AddDays(1).DayOfWeek;
            var evalOptimized = new ScheduleEvaluationOptimized(new ScheduleRule()
                .ExecuteEveryDayOfWeek(firstDay, secondDay)
                .WithUtc()
                .Execute((e, c) => { return true; }));
            Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime));
            Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime.AddDays(1)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddDays(2)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddHours(1)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddMinutes(1)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddSeconds(1)));

            evalOptimized = new ScheduleEvaluationOptimized(new ScheduleRule()
                .ExecuteEveryDayOfWeek()
                .WithUtc()
                .Execute((e, c) => { return true; }));
            for(int i=0; i <7;i++)
                Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime.AddDays(i)));
        }

        [TestMethod]
        public void ExecuteEveryHoursTest()
        {
            var evalOptimized = new ScheduleEvaluationOptimized(new ScheduleRule()
                .ExecuteEveryHour(0, 1)
                .WithUtc()
                .Execute((e, c) => { return true; }));
            var evalTime = new DateTimeOffset(ScheduleRule.MinYear + 1, 1, 1, 0, 0, 0, TimeSpan.Zero);
            Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime));
            Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime.AddHours(1)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddHours(2)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddMinutes(1)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddSeconds(1)));

            evalOptimized = new ScheduleEvaluationOptimized(new ScheduleRule()
                .ExecuteEveryHour()
                .WithUtc()
                .Execute((e, c) => { return true; }));
            for(int i=0;i<60;i++)
                Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime.AddHours(i)));
            
            // Ensure only executes at zeroth minute 
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddMinutes(3)));
        }

        [TestMethod]
        public void ExecuteEveryMinutesTest()
        {
            var evalOptimized = new ScheduleEvaluationOptimized(new ScheduleRule()
                .ExecuteEveryMinute(0, 1)
                .WithUtc()
                .Execute((e, c) => { return true; }));
            var evalTime = new DateTimeOffset(ScheduleRule.MinYear + 1, 1, 1, 0, 0, 0, TimeSpan.Zero);
            Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime));
            Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime.AddMinutes(1)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddMinutes(2)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddSeconds(1)));

            evalOptimized = new ScheduleEvaluationOptimized(new ScheduleRule()
                .ExecuteEveryMinute()
                .WithUtc()
                .Execute((e, c) => { return true; }));
            for(int i=0;i<60;i++)
                Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime.AddMinutes(i)));
        }

        [TestMethod]
        public void ExecuteEverySecondsText()
        {
            var evalOptimized = new ScheduleEvaluationOptimized(new ScheduleRule()
                .ExecuteEverySecond(0, 1)
                .WithUtc()
                .Execute((e, c) => { return true; }));
            var evalTime = new DateTimeOffset(ScheduleRule.MinYear + 1, 1, 1, 0, 0, 0, TimeSpan.Zero);
            for(int i=0; i <10;i++)
                Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime
                    .AddYears(i)
                    .AddMonths(i)
                    .AddDays(i)
                    .AddHours(i)
                    .AddMinutes(i)));
            Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime.AddSeconds(1)));
            Assert.IsFalse(evalOptimized.EvaluateRuleMatch(evalTime.AddSeconds(2)));

            evalOptimized = new ScheduleEvaluationOptimized(new ScheduleRule()
                            .ExecuteEverySecond()
                            .WithUtc()
                            .Execute((e, c) => { return true; }));
            for(int i = 0; i <10;i++)
                Assert.IsTrue(evalOptimized.EvaluateRuleMatch(evalTime.AddSeconds(i)));
        }



        [TestMethod]
        public void Bounds_Month_Min()
        {
            Throws<ArgumentOutOfRangeException>(() => new ScheduleRule()
                .AtMonths(0));
        }

        [TestMethod]
        public void Bounds_Month_Max()
        {
            Throws<ArgumentOutOfRangeException>(() => new ScheduleRule()
                .AtMonths(13));
        }

        [TestMethod]
        public void Bounds_DaysOfMonth_Min()
        {
            Throws<ArgumentOutOfRangeException>(() => new ScheduleRule()
                .AtDaysOfMonth(0));
        }

        [TestMethod]
        public void Bounds_DaysOfMonth_Max()
        {
            Throws<ArgumentOutOfRangeException>(() => new ScheduleRule()
                .AtDaysOfMonth(32));
        }

        [TestMethod]
        public void Bounds_DaysOfWeek_Min()
        {
            Throws<ArgumentOutOfRangeException>(() => new ScheduleRule()
                .AtDaysOfWeek(-1));
        }

        [TestMethod]
        public void Bounds_DaysOfWeek_Max()
        {
            Throws<ArgumentOutOfRangeException>(() => new ScheduleRule()
                .AtDaysOfWeek(7));
        }

        [TestMethod]
        public void Bounds_Hours_Min()
        {
            Throws<ArgumentOutOfRangeException>(() => new ScheduleRule()
                .AtHours(-1));
        }

        [TestMethod]
        public void Bounds_Hours_Max()
        {
            Throws<ArgumentOutOfRangeException>(() => new ScheduleRule()
                .AtHours(24));
        }

        [TestMethod]
        public void Bounds_Minutes_Min()
        {
            Throws<ArgumentOutOfRangeException>(() => new ScheduleRule()
                .AtMinutes(-1));
        }

        [TestMethod]
        public void Bounds_Minutes_Max()
        {
            Throws<ArgumentOutOfRangeException>(() => new ScheduleRule()
                .AtMinutes(60));
        }

        [TestMethod]
        public void Bounds_Seconds_Min()
        {
            Throws<ArgumentOutOfRangeException>(() => new ScheduleRule()
                .AtSeconds(-1));
        }

        [TestMethod]
        public void Bounds_Seconds_Max()
        {
            Throws<ArgumentOutOfRangeException>(() => new ScheduleRule()
                .AtSeconds(60));
        }

        [TestMethod]
        public void Cron_Base_Case()
        {
            var rule = new ScheduleRule()
                .FromCron("55,03 22,19,20 2,03,11,29,31 2,03,11,12 0,5,6");

            Assert.HasCount(2, rule.Minutes);
            Assert.IsTrue(rule.Minutes.Contains(55));
            Assert.IsTrue(rule.Minutes.Contains(3));

            Assert.HasCount(3, rule.Hours);
            Assert.IsTrue(rule.Hours.Contains(22));
            Assert.IsTrue(rule.Hours.Contains(19));
            Assert.IsTrue(rule.Hours.Contains(20));

            Assert.HasCount(5, rule.DaysOfMonth);
            Assert.IsTrue(rule.DaysOfMonth.Contains(2));
            Assert.IsTrue(rule.DaysOfMonth.Contains(3));
            Assert.IsTrue(rule.DaysOfMonth.Contains(11));
            Assert.IsTrue(rule.DaysOfMonth.Contains(29));
            Assert.IsTrue(rule.DaysOfMonth.Contains(31));

            Assert.HasCount(4, rule.Months);
            Assert.IsTrue(rule.Months.Contains(2));
            Assert.IsTrue(rule.Months.Contains(3));
            Assert.IsTrue(rule.Months.Contains(11));
            Assert.IsTrue(rule.Months.Contains(12));

            Assert.HasCount(3, rule.DaysOfWeek);
            Assert.IsTrue(rule.DaysOfWeek.Contains(0));
            Assert.IsTrue(rule.DaysOfWeek.Contains(5));
            Assert.IsTrue(rule.DaysOfWeek.Contains(6));
            CollectionAssert.AreEqual(new int[] { 0 }, rule.Seconds);
        }

        [TestMethod]
        public void Cron_Stars()
        {
            var rule = new ScheduleRule()
                .FromCron("* * * *\t*");

            Assert.IsEmpty(rule.Minutes);
            Assert.IsEmpty(rule.Hours);
            Assert.IsEmpty(rule.DaysOfMonth);
            Assert.IsEmpty(rule.Months);
            Assert.IsEmpty(rule.DaysOfWeek);
        }

        [TestMethod]
        public void Cron_OutOfBounds()
        {
            Throws<ArgumentException>(() => new ScheduleRule()
                .FromCron("61 * * *\t*"));
        }

        [TestMethod]
        public void NullScheduleInputsThrowArgumentNullException()
        {
            var rule = new ScheduleRule();

            // Null values used to drift into later optimization/evaluation code. These checks make
            // invalid schedules fail at the API boundary instead of poisoning the runtime loop.
            Throws<ArgumentNullException>(() => rule.FromCron(null));
            Throws<ArgumentNullException>(() => rule.AtYears(null));
            Throws<ArgumentNullException>(() => rule.AtMonths(null));
            Throws<ArgumentNullException>(() => rule.AtDaysOfMonth(null));
            Throws<ArgumentNullException>(() => rule.AtDaysOfWeek(null));
            Throws<ArgumentNullException>(() => rule.AtHours(null));
            Throws<ArgumentNullException>(() => rule.AtMinutes(null));
            Throws<ArgumentNullException>(() => rule.AtSeconds(null));
            Throws<ArgumentNullException>(() => rule.WithTimeZone((string)null));
            Throws<ArgumentNullException>(() => rule.WithTimeZone((TimeZoneInfo)null));
            Throws<ArgumentNullException>(() => rule.Execute((Func<ScheduleRuleMatchEventArgs, System.Threading.CancellationToken, Task<bool>>)null));
            Throws<ArgumentNullException>(() => rule.Execute((Func<ScheduleRuleMatchEventArgs, System.Threading.CancellationToken, bool>)null));
            Throws<ArgumentNullException>(() => rule.Execute((IScheduledTask)null));
            Throws<ArgumentNullException>(() => rule.ExecuteAndRetry((Func<ScheduleRuleMatchEventArgs, System.Threading.CancellationToken, Task<bool>>)null, 1, 2));
            Throws<ArgumentNullException>(() => rule.ExecuteAndRetry((Func<ScheduleRuleMatchEventArgs, System.Threading.CancellationToken, bool>)null, 1, 2));
        }

        [TestMethod]
        public void TimeZoneTests()
        {
            // Default - UTC
            var rule = new ScheduleRule()
                .AtSeconds(0)
                .AtMinutes(0)
                .AtHours(11)
                .Execute((a, b) => true);

            Assert.IsTrue(new ScheduleEvaluationOptimized(rule).EvaluateRuleMatch(new DateTimeOffset(DateTime.Now.Year, 6, 19, 11, 0, 0, TimeSpan.Zero)));

            // Evaluate against an offset - rule still UTC
            Assert.IsFalse(new ScheduleEvaluationOptimized(rule).EvaluateRuleMatch(new DateTimeOffset(DateTime.Now.Year, 6, 19, 11, 0, 0, TimeSpan.FromHours(-8))));
            Assert.IsTrue(new ScheduleEvaluationOptimized(rule).EvaluateRuleMatch(new DateTimeOffset(DateTime.Now.Year, 6, 19, 3, 0, 0, TimeSpan.FromHours(-8))));

            // Adjust rule to a different time zone
            // PST is -8 in winter and -7 in summer
            var pst = TimeZoneInfo.CreateCustomTimeZone(
                "TestPacific", TimeSpan.FromHours(-7), "Test Pacific", "Test Pacific");
            rule.WithTimeZone(pst);
            Assert.IsTrue(new ScheduleEvaluationOptimized(rule).EvaluateRuleMatch(new DateTimeOffset(DateTime.Now.Year, 6, 19, 11, 0, 0, TimeSpan.FromHours(-7))));
            Assert.IsFalse(new ScheduleEvaluationOptimized(rule).EvaluateRuleMatch(new DateTimeOffset(DateTime.Now.Year, 6, 19, 11, 0, 0, TimeSpan.Zero)));
        }
       
    }
}
