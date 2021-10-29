/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TaskSchedulerEngine
{
    public class ConsoleWriteTask : ITask
    {
        public void OnScheduleRuleMatch(object sender, ScheduleRuleMatchEventArgs e)
        {
            Console.WriteLine("{0}: Event intended for {1:o} occured at {2:o}", e.TaskId, e.TimeScheduledUtc, e.TimeSignaledUtc);
        }

    }
}
