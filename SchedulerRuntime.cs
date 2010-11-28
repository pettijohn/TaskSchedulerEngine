using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace TaskSchedulerEngine
{
    public static class SchedulerRuntime
    {
        public static void Start()
        {
            TaskEvaluationPump pump = TaskEvaluationPump.GetInstance();
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

        public static Dictionary<string, ScheduleDefinition> Schedule
        {
            get
            {
                TaskEvaluationPump pump = TaskEvaluationPump.GetInstance();
                return pump.schedule;
            }
        }

        public static ScheduleDefinition GetSchedule(string name)
        {
            TaskEvaluationPump pump = TaskEvaluationPump.GetInstance();
            return pump.GetSchedule(name);
        }
    }
}
