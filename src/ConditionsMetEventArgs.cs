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
    public class ConditionsMetEventArgs : EventArgs
    {
        public DateTime TimeSignaledUtc { get; set; }
        public DateTime TimeScheduledUtc { get; set; }
        public int TaskId { get; set; }
        public ScheduleDefinition ScheduleDefinition { get; set; }
    }
}
