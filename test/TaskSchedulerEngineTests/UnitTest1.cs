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
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var s = new Schedule()
                //.AtSeconds(0, 10, 20, 30, 40, 50)
                .WithLocalTime()
                .Execute<ConsoleWriteTask>();
            SchedulerRuntime.Start(s);

            Thread.Sleep(new TimeSpan(0, 0, 11));

            SchedulerRuntime.Stop();
        }
    }
}
