using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TaskSchedulerEngine.Fluent;

namespace TaskSchedulerEngine
{
    public static class SchedulerRuntime
    {
        public static void Start(Schedule schedule)
        {
            Start(new Schedule[] { schedule });
        }

        public static void Start(IEnumerable<Schedule> schedule)
        {
            TaskEvaluationPump pump = TaskEvaluationPump.GetInstance();
            pump.Initialize(schedule);
            pump.Pump();
        }

        public static void Stop()
        {
            TaskEvaluationPump pump = TaskEvaluationPump.GetInstance();
            //Request that it stop running.
            pump.Running.Value = false;

            //Wait for it to stop running.
            while (pump.Stopped.Value == false)
            {
                Thread.Sleep(10);
            }
        }

    }
}
