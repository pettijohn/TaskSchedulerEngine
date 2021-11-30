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
                .WithUtc();

            var evalOptimized = new ScheduleEvaluationOptimized(rule);

            var evalTime = new DateTime(2021, 11, 30, 23, 0, 0, DateTimeKind.Utc);

            var testResult = evalOptimized.Evaluate(evalTime);
            Assert.IsNotNull(testResult);
            Assert.AreEqual(evalTime, testResult.TimeScheduledUtc);
        }
    }
}