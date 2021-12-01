using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaskSchedulerEngine;
using System.Threading;
using System.Globalization;

namespace SchedulerEngineRuntimeTests
{
    /// <summary>
    /// Test both human and machine optimized schedule rules. 
    /// </summary>
    [TestClass]
    public class TaskEvaluationRuntimeTest
    {
        public TaskEvaluationRuntimeTest()
        { }

        [TestMethod]
        public void SingleExecutionOnSeparateThreads()
        {
            var testThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            //Set initial value to same as above, which will fail test if callback fails to update
            int callbackThread = testThread;

            var runtime = new TaskEvaluationRuntime();
            runtime.AddSchedule(new ScheduleRule()
                .Execute((e, token) => {
                    callbackThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                    e.Runtime.DeleteSchedule(e.ScheduleRule);
                }));

            var task = runtime.RunAsync();
            Thread.Sleep(1200);
            runtime.RequestStop();
            task.Wait();

            Assert.AreNotEqual(testThread, callbackThread);
        }

        [TestMethod]
        public void TwoLongRunningTasksExecuteSimultaneouslyAndGracefulShutdown()
        {
            bool executed1 = false;
            bool executed2 = false;
            bool gracefulShutdown1 = false;
            bool gracefulShutdown2 = false;

            var runtime = new TaskEvaluationRuntime();
            runtime.AddSchedule(new ScheduleRule()
                .Execute((e, token) => {
                    executed1 = true;
                    e.Runtime.DeleteSchedule(e.ScheduleRule);
                    Thread.Sleep(3000);
                    gracefulShutdown1 = true;
                }));

            runtime.AddSchedule(new ScheduleRule()
                .Execute((e, token) => {
                    executed2 = true;
                    e.Runtime.DeleteSchedule(e.ScheduleRule);
                    Thread.Sleep(3000);
                    gracefulShutdown2 = true;
                }));

            var task = runtime.RunAsync();
            Thread.Sleep(1200); // sleep here req'd otherwise race condition will stop before start
            runtime.RequestStop();
            task.Wait();

            Assert.IsNotNull(executed1);
            Assert.IsTrue(executed2);
            Assert.IsTrue(gracefulShutdown1);
            Assert.IsTrue(gracefulShutdown2);
        }

    }
}