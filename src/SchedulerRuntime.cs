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
        // Allow user to start without any schedule and gradually add one as needed
        public static void Start()
        {
            TaskEvaluationPump pump = TaskEvaluationPump.GetInstance();
            pump.Pump();
        }

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

        public static void StartWithConfig(string configSection = "taskSchedulerEngine")
        {
            TaskEvaluationPump pump = TaskEvaluationPump.GetInstance();
            pump.InitializeFromConfig(configSection);
            pump.Pump();
        }

        public static void Stop()
        {
            TaskEvaluationPump pump = TaskEvaluationPump.GetInstance();
            pump.Stop();
        }

        public static IEnumerable<string> ListScheduleName()
        {
            TaskEvaluationPump pump = TaskEvaluationPump.GetInstance();
            return pump.ListScheduleName();
        }

        public static bool AddSchedule(Schedule schedule)
        {
            TaskEvaluationPump pump = TaskEvaluationPump.GetInstance();
            return pump.AddSchedule(schedule);
        }

        public static bool UpdateSchedule(Schedule schedule)
        {
            TaskEvaluationPump pump = TaskEvaluationPump.GetInstance();
            return pump.UpdateSchedule(schedule);
        }

        public static bool DeleteSchedule(string scheduleName)
        {
            TaskEvaluationPump pump = TaskEvaluationPump.GetInstance();
            return pump.DeleteSchedule(scheduleName);
        }
    }
}
