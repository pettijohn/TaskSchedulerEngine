using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaskSchedulerEngine;
using System.Threading;

namespace SchedulerEngineRuntimeTests
{
    /// <summary>
    /// Test both human and machine optimized schedule rules. 
    /// </summary>
    [TestClass]
    public class ScheduleRuleTest
    {
        public ScheduleRuleTest()
        {}

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
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void BitfieldOverflowException()
        {
            ScheduleEvaluationOptimized.ParseIntArrayToBitfield(new int[] { 63 });
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
                .AtDaysOfWeek(3)
                .AtDaysOfMonth(30)
                .AtMonths(11)
                .AtYears(2022)
                .WithName("Optional name/ID parameter")
                .WithUtc()
                .Execute((e, c) => { return true; }); //noop callback, as callback cannot be null

            var evalOptimized = new ScheduleEvaluationOptimized(rule);

            var evalTime = new DateTime(2022, 11, 30, 23, 0, 0, DateTimeKind.Utc);

            var testResult = evalOptimized.EvaluateRuleMatch(evalTime);
            Assert.IsTrue(testResult);
        }

        [TestMethod]
        public void ExecuteOnceTest()
        {
            var executeTime = new DateTime(2022, 11, 30, 23, 0, 0, DateTimeKind.Utc);
            var rule = new ScheduleRule()
                .ExecuteOnce(executeTime)
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
            testResult = evalOptimized.EvaluateRuleMatch(evalTime.AddYears(1));
            Assert.IsFalse(testResult);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Bounds_Month_Min()
        {
            var rule = new ScheduleRule()
                .AtMonths(0);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Bounds_Month_Max()
        {
            var rule = new ScheduleRule()
                .AtMonths(13);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Bounds_DaysOfMonth_Min()
        {
            var rule = new ScheduleRule()
                .AtDaysOfMonth(0);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Bounds_DaysOfMonth_Max()
        {
            var rule = new ScheduleRule()
                .AtDaysOfMonth(32);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Bounds_DaysOfWeek_Min()
        {
            var rule = new ScheduleRule()
                .AtDaysOfWeek(-1);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Bounds_DaysOfWeek_Max()
        {
            var rule = new ScheduleRule()
                .AtDaysOfWeek(7);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Bounds_Hours_Min()
        {
            var rule = new ScheduleRule()
                .AtHours(-1);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Bounds_Hours_Max()
        {
            var rule = new ScheduleRule()
                .AtHours(24);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Bounds_Minutes_Min()
        {
            var rule = new ScheduleRule()
                .AtMinutes(-1);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Bounds_Minutes_Max()
        {
            var rule = new ScheduleRule()
                .AtMinutes(60);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Bounds_Seconds_Min()
        {
            var rule = new ScheduleRule()
                .AtSeconds(-1);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Bounds_Seconds_Max()
        {
            var rule = new ScheduleRule()
                .AtSeconds(60);
        }
    }
}