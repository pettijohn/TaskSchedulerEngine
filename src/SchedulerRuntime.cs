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
using TaskSchedulerEngine.Fluent;

namespace TaskSchedulerEngine
{
    public class SchedulerRuntime
    {
        /// <summary>
        /// Creates a new Scheduler Runtime and begins the evaluation pump that runs every second
        /// </summary>
        public SchedulerRuntime()
        {
            TaskEvaluationPump pump = TaskEvaluationPump.GetInstance();
            pump.Pump();
        }

        public void Add(Schedule schedule)
        {
            Add(new Schedule[] { schedule });
        }

        /// <summary>
        /// Add a range of user-friendly <see cref="Schedule"/> to the current <see cref="TaskEvaluationPump"/>
        /// </summary>
        public void Add(IEnumerable<Schedule> schedule)
        {
            TaskEvaluationPump pump = TaskEvaluationPump.GetInstance();
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

        /// <summary>
        /// Add a single user-friendly <see cref="Schedule"/> to the current <see cref="TaskEvaluationPump"/>
        /// </summary>
        /// <param name="schedule"></param>
        public static bool AddSchedule(Schedule schedule)
        {
            TaskEvaluationPump pump = TaskEvaluationPump.GetInstance();
            return pump.AddRange(schedule);
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
