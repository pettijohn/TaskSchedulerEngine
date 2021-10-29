/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace TaskSchedulerEngine
{
    public static class SchedulerRuntime
    {
        // Allow user to start without any schedule and gradually add one as needed
        public static void Start()
        {
            TaskEvaluationPump pump = TaskEvaluationPump.GetInstance();
            pump.Initialize(new ScheduleRule[0]);
            pump.Pump();
        }

        public static void Start(ScheduleRule schedule)
        {
            Start(new ScheduleRule[] { schedule });
        }

        public static void Start(IEnumerable<ScheduleRule> schedule)
        {
            TaskEvaluationPump pump = TaskEvaluationPump.GetInstance();
            pump.Initialize(schedule);
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

        public static bool AddSchedule(ScheduleRule schedule)
        {
            TaskEvaluationPump pump = TaskEvaluationPump.GetInstance();
            return pump.AddSchedule(schedule);
        }

        public static bool UpdateSchedule(ScheduleRule schedule)
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
