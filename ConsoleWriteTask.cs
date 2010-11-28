using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TaskSchedulerEngine
{
    public class ConsoleWriteTask : ITask
    {
        #region ITask Members

        public void HandleConditionsMetEvent(object sender, ConditionsMetEventArgs e)
        {
            Console.WriteLine("{0}: Event intended for {1:o} occured at {2:o}", e.TaskId, e.TimeScheduledUtc, e.TimeSignaledUtc);
            //ITask nextTask = (ITask)Activator.CreateInstance(typeof(ConsoleWriteTask));
            //e.ScheduleDefinition.ConditionsMet += nextTask.HandleConditionsMetEvent;
        }

        public void Initialize(ScheduleDefinition schedule, string parameters)
        {
            //Do nothing
        }

        #endregion
    }
}
