using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaskSchedulerEngine;
using TaskSchedulerEngine.Fluent;
using System.Threading;

namespace SchedulerEngineRuntimeTests
{
    [TestClass]
    public class FluentTest
    {
        [TestMethod]
        public void FluentTest1()
        {

            //Verify output. Should see one or two 10-second ticks, one or two 1,6 ticks, and one or two 2,7 ticks.
            var s = new Schedule()
                .AtSeconds(0, 10, 20, 30, 40, 50)
                .WithLocalTime()
                .Execute<TenSecTask>();
            SchedulerRuntime.Add(s);

            Thread.Sleep(new TimeSpan(0, 0, 2));

            SchedulerRuntime.AddSchedule(new Schedule().WithName("OneSix").AtSeconds(1, 6, 11, 16, 21, 26, 31, 36, 41, 46, 51, 56).Execute<OneSixTask>());

            Thread.Sleep(new TimeSpan(0, 0, 6));

            SchedulerRuntime.UpdateSchedule(new Schedule().WithName("OneSix").AtSeconds(2, 7, 12, 17, 22, 27, 32, 37, 42, 47, 52, 57).Execute<TwoSevenTask>());

            Thread.Sleep(new TimeSpan(0, 0, 6));

            SchedulerRuntime.Stop();
            Console.WriteLine("Stopped");

            Assert.IsTrue(TenSecTask.Ticked);
            Assert.IsTrue(OneSixTask.Ticked);
            Assert.IsTrue(TwoSevenTask.Ticked);
        }

        class TenSecTask : ITask
        {
            public static bool Ticked = false;
            public void Tick(object sender, TickEventArgs e)
            {
                if (!new int[] {0, 10, 20, 30, 40, 50}.Contains(e.TimeScheduledUtc.Second))
                    throw new InvalidOperationException();

                Ticked = true;
            }

            public void Initialize(BitwiseSchedule schedule, object parameters)
            {
            }
        }

        class OneSixTask : ITask
        {
            public static bool Ticked = false;
            public void Tick(object sender, TickEventArgs e)
            {
                if (!new int[] { 1, 6, 11, 16, 21, 26, 31, 36, 41, 46, 51, 56 }.Contains(e.TimeScheduledUtc.Second))
                    throw new InvalidOperationException();

                Ticked = true;
            }

            public void Initialize(BitwiseSchedule schedule, object parameters)
            {
            }
        }

        class TwoSevenTask : ITask
        {
            public static bool Ticked = false;
            public void Tick(object sender, TickEventArgs e)
            {
                if (!new int[] { 2, 7, 12, 17, 22, 27, 32, 37, 42, 47, 52, 57 }.Contains(e.TimeScheduledUtc.Second))
                    throw new InvalidOperationException();

                Ticked = true;
            }

            public void Initialize(BitwiseSchedule schedule, object parameters)
            {
            }
        }


    }
}
