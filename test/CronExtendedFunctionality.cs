using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaskSchedulerEngine;
using static SchedulerEngineRuntimeTests.TestAssert;

namespace SchedulerEngineRuntimeTests
{
    [TestClass]
    public class CronExtendedFunctionality
    {
        [TestMethod]
        public void CronField_Wildcard_ReturnsEmptyWildcard()
        {
            var results = ScheduleRule.ParseCronField("*");

            Assert.IsEmpty(results);
        }

        [TestMethod]
        public void CronField_SingleValue_ReturnsSingleValue()
        {
            var results = ScheduleRule.ParseCronField("20", 59);

            CollectionAssert.AreEqual(new int[] { 20 }, results);
        }

        [TestMethod]
        public void CronField_List_ReturnsValuesInOrder()
        {
            var results = ScheduleRule.ParseCronField("1,2,3,4", 59);

            CollectionAssert.AreEqual(new int[] { 1, 2, 3, 4 }, results);
        }

        [TestMethod]
        public void CronField_Range_ReturnsInclusiveValues()
        {
            var results = ScheduleRule.ParseCronField("8-12", 59);

            CollectionAssert.AreEqual(new int[] { 8, 9, 10, 11, 12 }, results);
        }

        [TestMethod]
        public void CronField_RangeWithStep_ReturnsCronStepValues()
        {
            var results = ScheduleRule.ParseCronField("8-14/3", 59);

            CollectionAssert.AreEqual(new int[] { 8, 11, 14 }, results);
        }

        [TestMethod]
        public void CronField_WildcardWithStep_ReturnsCronStepValues()
        {
            var results = ScheduleRule.ParseCronField("*/20", 59);

            CollectionAssert.AreEqual(new int[] { 0, 20, 40 }, results);
        }

        [TestMethod]
        public void CronField_StartWithStep_ReturnsCronStepValues()
        {
            var results = ScheduleRule.ParseCronField("1/10", 59);

            CollectionAssert.AreEqual(new int[] { 1, 11, 21, 31, 41, 51 }, results);
        }

        [TestMethod]
        public void CronField_ListWithRangesAndSteps_ReturnsExpandedValues()
        {
            var results = ScheduleRule.ParseCronField("1,5-7,10-16/3", 59);

            CollectionAssert.AreEqual(new int[] { 1, 5, 6, 7, 10, 13, 16 }, results);
        }

        [TestMethod]
        public void CronField_WithLowerLimit_UsesLowerLimitForWildcardStep()
        {
            var results = ScheduleRule.ParseCronField("*/2", 12, 1);

            CollectionAssert.AreEqual(new int[] { 1, 3, 5, 7, 9, 11 }, results);
        }

        [TestMethod]
        public void CronField_BoundaryValues_ReturnsValuesAtLimits()
        {
            CollectionAssert.AreEqual(new int[] { 0, 59 }, ScheduleRule.ParseCronField("0,59", 59));
            CollectionAssert.AreEqual(new int[] { 0, 23 }, ScheduleRule.ParseCronField("0,23", 23));
            CollectionAssert.AreEqual(new int[] { 1, 31 }, ScheduleRule.ParseCronField("1,31", 31, 1));
            CollectionAssert.AreEqual(new int[] { 1, 12 }, ScheduleRule.ParseCronField("1,12", 12, 1));
            CollectionAssert.AreEqual(new int[] { 0, 6 }, ScheduleRule.ParseCronField("0,6", 6));
        }

        [TestMethod]
        public void CronField_StepEdges_ReturnsCronStepValues()
        {
            CollectionAssert.AreEqual(new int[] { 58 }, ScheduleRule.ParseCronField("58/2", 59));
            CollectionAssert.AreEqual(new int[] { 0 }, ScheduleRule.ParseCronField("*/60", 59));
            CollectionAssert.AreEqual(new int[] { 0, 4 }, ScheduleRule.ParseCronField("0-5/4", 59));
        }

        [TestMethod]
        public void FromCron_WithRangesAndSteps_ExpandsFields()
        {
            var rule = new ScheduleRule()
                .FromCron("*/15 9-17/4 1,15 */3 1-5");

            CollectionAssert.AreEqual(new int[] { 0 }, rule.Seconds);
            CollectionAssert.AreEqual(new int[] { 0, 15, 30, 45 }, rule.Minutes);
            CollectionAssert.AreEqual(new int[] { 9, 13, 17 }, rule.Hours);
            CollectionAssert.AreEqual(new int[] { 1, 15 }, rule.DaysOfMonth);
            CollectionAssert.AreEqual(new int[] { 1, 4, 7, 10 }, rule.Months);
            CollectionAssert.AreEqual(new int[] { 1, 2, 3, 4, 5 }, rule.DaysOfWeek);
        }

        [TestMethod]
        public void FromCron_WithFlexibleWhitespace_ParsesFields()
        {
            var rule = new ScheduleRule()
                .FromCron("  0\t12\n1 1\r0  ");

            CollectionAssert.AreEqual(new int[] { 0 }, rule.Minutes);
            CollectionAssert.AreEqual(new int[] { 12 }, rule.Hours);
            CollectionAssert.AreEqual(new int[] { 1 }, rule.DaysOfMonth);
            CollectionAssert.AreEqual(new int[] { 1 }, rule.Months);
            CollectionAssert.AreEqual(new int[] { 0 }, rule.DaysOfWeek);
        }

        [TestMethod]
        public void FromCron_WithInvalidFieldCount_ThrowsArgumentException()
        {
            Throws<ArgumentException>(() => new ScheduleRule().FromCron("0 * * *"));
            Throws<ArgumentException>(() => new ScheduleRule().FromCron("0 * * * * *"));
        }

        [TestMethod]
        public void CronField_OutOfBounds_ThrowsArgumentOutOfRangeException()
        {
            Throws<ArgumentOutOfRangeException>(() => ScheduleRule.ParseCronField("60", 59));
            Throws<ArgumentOutOfRangeException>(() => ScheduleRule.ParseCronField("0", 12, 1));
            Throws<ArgumentOutOfRangeException>(() => ScheduleRule.ParseCronField("*/61", 59));
        }

        [TestMethod]
        public void CronField_MalformedExpression_ThrowsArgumentException()
        {
            Throws<ArgumentException>(() => ScheduleRule.ParseCronField(null));
            Throws<ArgumentException>(() => ScheduleRule.ParseCronField(""));
            Throws<ArgumentException>(() => ScheduleRule.ParseCronField("1,2,"));
            Throws<ArgumentException>(() => ScheduleRule.ParseCronField(",1,2"));
            Throws<ArgumentException>(() => ScheduleRule.ParseCronField("3-"));
            Throws<ArgumentException>(() => ScheduleRule.ParseCronField("-3"));
            Throws<ArgumentException>(() => ScheduleRule.ParseCronField("-1"));
            Throws<ArgumentException>(() => ScheduleRule.ParseCronField("4(4"));
            Throws<ArgumentException>(() => ScheduleRule.ParseCronField("1 2"));
            Throws<ArgumentException>(() => ScheduleRule.ParseCronField("1//2"));
            Throws<ArgumentException>(() => ScheduleRule.ParseCronField("1/0"));
            Throws<ArgumentException>(() => ScheduleRule.ParseCronField("10-5"));
            Throws<ArgumentException>(() => ScheduleRule.ParseCronField("1,*"));
            Throws<ArgumentException>(() => ScheduleRule.ParseCronField("/5"));
            Throws<ArgumentException>(() => ScheduleRule.ParseCronField("*/"));
            Throws<ArgumentException>(() => ScheduleRule.ParseCronField("1/"));
            Throws<ArgumentException>(() => ScheduleRule.ParseCronField("1-2-3"));
            Throws<ArgumentException>(() => ScheduleRule.ParseCronField("1/-2"));
        }
    }
}
