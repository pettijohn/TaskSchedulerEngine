using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskSchedulerEngine;
using static SchedulerEngineRuntimeTests.TestAssert;

namespace TaskSchedulerEngineTests {
    [TestClass]
    public class CronExtendedFunctionality {
        public CronExtendedFunctionality() { }

        [TestMethod("Simple Range")]
        public void Range1() {
            var expression = "0-3";
            var expected = new int[] { 0, 1, 2, 3 };
            var results = TaskSchedulerEngine.ScheduleRule.CalculateCronInts(expression);
            CollectionAssert.AreEqual(expected, results);
        }

        [TestMethod("Multi Digit Range")]
        public void Range3() {
            var expression = "0-12";
            var expected = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
            var results = TaskSchedulerEngine.ScheduleRule.CalculateCronInts(expression);
            CollectionAssert.AreEqual(expected, results);
        }

        [TestMethod("Simple Range 2")]
        public void Range2() {
            var expression = "0-5";
            var expected = new int[] { 0, 1, 2, 3, 4, 5 };
            var results = TaskSchedulerEngine.ScheduleRule.CalculateCronInts(expression);
            CollectionAssert.AreEqual(expected, results);
        }


        [TestMethod("Range with Nth Selector")]
        public void RangeWithNth1() {
            var expression = "0-4/2";
            var expected = new int[] { 1, 3 };
            var results = TaskSchedulerEngine.ScheduleRule.CalculateCronInts(expression);
            CollectionAssert.AreEqual(expected, results);
        }

        [TestMethod("Multi Digit Range with Nth Selector 1")]
        public void RangeWithNth2() {
            var expression = "0-12/2";
            var expected = new int[] { 1, 3, 5, 7, 9, 11 };
            var results = TaskSchedulerEngine.ScheduleRule.CalculateCronInts(expression);
            CollectionAssert.AreEqual(expected, results);
        }

        [TestMethod("Nth Selector")]
        public void Nth1() {
            var expression = "50/1";
            var expected = new int[] { 50, 51, 52, 53, 54, 55, 56, 57, 58, 59 };
            var results = TaskSchedulerEngine.ScheduleRule.CalculateCronInts(expression, 59);
            CollectionAssert.AreEqual(expected, results);
        }

        [TestMethod("Nth Selector 3")]
        public void Nth3() {
            var expression = "0,1/20";
            var expected = new int[] { 0, 20, 40 };
            var results = TaskSchedulerEngine.ScheduleRule.CalculateCronInts(expression, 59);
            CollectionAssert.AreEqual(expected, results);
        }

        [TestMethod("Nth Selector 2")]
        public void Nth2() {
            var expression = "50/2";
            var expected = new int[] { 51, 53, 55, 57, 59 };
            var results = TaskSchedulerEngine.ScheduleRule.CalculateCronInts(expression, 59);
            CollectionAssert.AreEqual(expected, results);
        }

        [TestMethod("Multi Digit Nth Selector")]
        public void MultiDigitNth() {
            var expression = "1/10";
            var expected = new int[] { 10, 20, 30, 40, 50 };
            var results = TaskSchedulerEngine.ScheduleRule.CalculateCronInts(expression, 59);
            CollectionAssert.AreEqual(expected, results);
        }

        [TestMethod("Simple List")]
        public void List1() {
            var expression = "1,2,3,4";
            var expected = new int[] { 1, 2, 3, 4, };
            var results = TaskSchedulerEngine.ScheduleRule.CalculateCronInts(expression, 59);
            CollectionAssert.AreEqual(expected, results);
        }

        [TestMethod("Mixed Test 1")]
        public void MixedTest1() {
            var expression = "1,2,3,4-10";
            var expected = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 , 10};
            var results = TaskSchedulerEngine.ScheduleRule.CalculateCronInts(expression, 59);
            CollectionAssert.AreEqual(expected, results);
        }

        [TestMethod("Mixed Test 2")]
        public void MixedTest2() {
            var expression = "20";
            var expected = new int[] { 20 };
            var results = TaskSchedulerEngine.ScheduleRule.CalculateCronInts(expression, 59);
            CollectionAssert.AreEqual(expected, results);
        }

        [TestMethod("Full Cron 1")]
        public void CronFull1() {
            var rule = new ScheduleRule()
                .FromCron("0 2-3 * * *")
                .Execute((a, b) => true);
        }

        [TestMethod("Full Cron 2")]
        public void CronFull2() {
            var rule = new ScheduleRule()
                .FromCron("0 1/1 * * *")
                .Execute((a, b) => true);
        }


        [TestMethod("Full Cron 3")]
        public void CronFull3() {
            var rule = new ScheduleRule()
                .FromCron("59 * * * *")
                .Execute((a, b) => true);
        }

        // FAILIURES

        [TestMethod("Fail 1")]
        public void Fail1() {
            var expression = ",1,2,3,4";
            Throws<ArgumentException>(() => TaskSchedulerEngine.ScheduleRule.CalculateCronInts(expression, 59));
        }

        [TestMethod("Fail 2")]
        public void Fail2() {
            var expression = "-3";
            Throws<ArgumentException>(() => TaskSchedulerEngine.ScheduleRule.CalculateCronInts(expression, 59));
        }

        [TestMethod("Fail 3")]
        public void Fail3() {
            var expression = "0,-3";
            Throws<ArgumentException>(() => TaskSchedulerEngine.ScheduleRule.CalculateCronInts(expression, 59));
        }

        [TestMethod("Fail 4")]
        public void Fail4() {
            var expression = "1,2,3,4,";
            Throws<ArgumentException>(() => TaskSchedulerEngine.ScheduleRule.CalculateCronInts(expression, 59));
        }

        [TestMethod("Fail 5")]
        public void Fail5() {
            var expression = "3-";
            Throws<ArgumentException>(() => TaskSchedulerEngine.ScheduleRule.CalculateCronInts(expression, 59));
        }

        [TestMethod("Fail 6")]
        public void Fail6() {
            var expression = "0,3-";
            Throws<ArgumentException>(() => TaskSchedulerEngine.ScheduleRule.CalculateCronInts(expression, 59));
        }

        [TestMethod("Fail 7")]
        public void Fail7() {
            var expression = "4(4";
            Throws<ArgumentException>(() => TaskSchedulerEngine.ScheduleRule.CalculateCronInts(expression, 59));
        }

    }
}
