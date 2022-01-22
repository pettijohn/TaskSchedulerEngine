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
                    return true;
                }));

            var task = runtime.RunAsync();
            Thread.Sleep(1200);
            runtime.RequestStop();
            task.Wait();

            Assert.AreNotEqual(testThread, callbackThread);
        }

        [TestMethod]
        public void RemoveExpired()
        {
            bool executed = false;
            var runtime = new TaskEvaluationRuntime();
            runtime.AddSchedule(new ScheduleRule()
                .ExpiresAfter(DateTimeOffset.Now.AddDays(-1))
                .Execute((e, token) => {
                    executed = true;
                    return true;
                }));

            var task = runtime.RunAsync();
            Thread.Sleep(1200);
            runtime.RequestStop();
            task.Wait();

            Assert.IsFalse(executed);
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
                    return true;
                }));

            runtime.AddSchedule(new ScheduleRule()
                .Execute((e, token) => {
                    executed2 = true;
                    e.Runtime.DeleteSchedule(e.ScheduleRule);
                    Thread.Sleep(3000);
                    gracefulShutdown2 = true;
                    return true;
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

        [TestMethod]
        public void ExponentialBackoffTaskTest()
        {
            var invoked = new List<DateTimeOffset>();
            var startFrom = DateTimeOffset.UtcNow.AddSeconds(1);
            var runtime = new TaskEvaluationRuntime();
            runtime.AddSchedule(new ScheduleRule()
                .ExecuteOnce(startFrom)
                .Execute(new ExponentialBackoffTask(
                    (e, _) => { 
                        // Do something that may fail like a network call - catch & gracefully fail by returning false.
                        // Exponential backoff task will retry up to maxAttempts times. 
                        invoked.Add(e.TimeScheduledUtc);
                        return false; 
                    },
                    4, // max attempts
                    2  // base retry interval
                )));
            
            // Retry logic: baseRetrySeconds*(2^retryCount) seconds
            // Task should execute at 0, 2, 4, 8 second intervals  

            var task = runtime.RunAsync();
            Thread.Sleep(40000); // wait for retries - long enough for an incorrect fifth invocation. 
            runtime.RequestStop();
            task.Wait();

            Console.WriteLine(startFrom);
            for (int i = 0; i < invoked.Count; i++)
            {
                Console.WriteLine($"{i}: {(invoked[i] - invoked[0]).TotalSeconds}");
            }

            Assert.AreEqual(4, invoked.Count);
            CollectionAssert.AllItemsAreUnique(invoked);
            // Should be about 14 seconds between first and last - with a little wiggle room 
            var span = invoked.Last() - invoked.First();
            Console.WriteLine($"Retry total duration: {span.TotalSeconds}");
            Assert.IsTrue(span > TimeSpan.FromSeconds(12) && span < TimeSpan.FromSeconds(16));
        }

    }
}