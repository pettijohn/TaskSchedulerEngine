using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaskSchedulerEngine;
using System.Threading;

namespace SchedulerEngineRuntimeTests
{
    [TestClass]
    public class FluentTest
    {
        [TestMethod]
        public void FluentTest1()
        {
            var host = new ServiceHost();
            var hostTask = host.RunAsync();
            //Verify output. Should see one or two 10-second ticks, one or two 1,6 ticks, and one or two 2,7 ticks.
            var s = new ScheduleRule()
                .AtSeconds(0, 10, 20, 30, 40, 50)
                .WithLocalTime()
                .Execute(new TenSecTask());

            Thread.Sleep(new TimeSpan(0, 0, 2));

            host.Pump.AddSchedule(new ScheduleRule().WithName("OneSix").AtSeconds(1, 6, 11, 16, 21, 26, 31, 36, 41, 46, 51, 56).Execute(new OneSixTask()));

            Thread.Sleep(new TimeSpan(0, 0, 6));

            host.Pump.UpdateSchedule(new ScheduleRule().WithName("OneSix").AtSeconds(2, 7, 12, 17, 22, 27, 32, 37, 42, 47, 52, 57).Execute(new OneSixTask()));

            Thread.Sleep(new TimeSpan(0, 0, 6));

            host.Pump.RequestStop();
            hostTask.Wait();
            Console.WriteLine("Stopped");

            Assert.IsTrue(TenSecTask.Ticked);
            Assert.IsTrue(OneSixTask.Ticked);
            Assert.IsTrue(TwoSevenTask.Ticked);
        }

        class TenSecTask : ITask
        {
            public static bool Ticked = false;
            public void OnScheduleRuleMatch(object sender, ScheduleRuleMatchEventArgs e)
            {
                if (!new int[] {0, 10, 20, 30, 40, 50}.Contains(e.TimeScheduledUtc.Second))
                    throw new InvalidOperationException();

                Ticked = true;
            }

        }

        class OneSixTask : ITask
        {
            public static bool Ticked = false;
            public void OnScheduleRuleMatch(object sender, ScheduleRuleMatchEventArgs e)
            {
                if (!new int[] { 1, 6, 11, 16, 21, 26, 31, 36, 41, 46, 51, 56 }.Contains(e.TimeScheduledUtc.Second))
                    throw new InvalidOperationException();

                Ticked = true;
            }
        }

        class TwoSevenTask : ITask
        {
            public static bool Ticked = false;
            public void OnScheduleRuleMatch(object sender, ScheduleRuleMatchEventArgs e)
            {
                if (!new int[] { 2, 7, 12, 17, 22, 27, 32, 37, 42, 47, 52, 57 }.Contains(e.TimeScheduledUtc.Second))
                    throw new InvalidOperationException();

                Ticked = true;
            }
        }


    }
}
