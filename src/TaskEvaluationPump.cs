using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TaskSchedulerEngine.Configuration;
using System.Configuration;

namespace TaskSchedulerEngine
{
    /// <summary>
    /// A worker thread wakes up at the start of every second and processes all of the At records.
    /// This class is a Singleton.
    /// </summary>
    internal class TaskEvaluationPump
    {
        /// <summary>
        /// Singleton to get the instance.
        /// </summary>
        public static TaskEvaluationPump GetInstance()
        {
            if(_instance == null)
            {
                lock(_singletonLock)
                {
                    if(_instance == null)
                    {
                        _instance = new TaskEvaluationPump();
                    }
                }
            }
            return _instance;
        }

        private static TaskEvaluationPump _instance;
        private static object _singletonLock = new object();

        public Dictionary<String,ScheduleDefinition> schedule { get; set; }

        /// <summary>
        /// Private constructor. Read from config and create ScheduleDefinitions from At objects, plus wire up delegates.
        /// </summary>
        private TaskEvaluationPump()
        {
            TaskSchedulerEngineConfigurationSection section = (TaskSchedulerEngineConfigurationSection)ConfigurationManager.GetSection("taskSchedulerEngine");
            this.schedule = new Dictionary<string, ScheduleDefinition>();

            foreach (At at in section.Schedule)
            {
                //Error-check for duplicate key.
                if (this.schedule.ContainsKey(at.Name))
                {
                    throw new ArgumentException(String.Format("You are attempting to add a duplicately-named Schedule to the Scheduling Engine: '{0}'", at.Name, "name"));
                }

                //Create an evaluation-friendly schedule from the config-friendly schedule.
                ScheduleDefinition schedule = new ScheduleDefinition(at);
                
                //Loop over all of the tasks associated with that schedule.
                foreach (Task task in at.Execute)
                {
                    //Create an instance.
                    object activated = Activator.CreateInstance(Type.GetType(task.Type));
                    ITask itask = activated as ITask;
                    if (itask == null)
                    {
                        throw new ArgumentException("Scheduled Tasks must be of Type ITask.");
                    }

                    //Wire up the delegate
                    schedule.ConditionsMet += itask.HandleConditionsMetEvent;
                    //Initialize the task.
                    itask.Initialize(schedule, task.Parameters);
                    //And attach it to its schedule.
                    schedule.Task = itask;
                }

                this.schedule.Add(schedule.Name, schedule);
            }
        }

        /// <summary>
        /// Gets the <see cref="ScheduleDefinition"/> specified by the <see cref="M:At.Name"/>
        /// </summary>
        /// <param name="scheduleName"></param>
        /// <returns></returns>
        internal ScheduleDefinition GetSchedule(string scheduleName)
        {
            return this.schedule.Values.Where(s => s.Name == scheduleName).First();
        }

        /// <summary>
        /// Hang onto the next second to evaluate. Thread-safe.
        /// </summary>
        private SerializedAccessProperty<DateTime> NextSecondToEvaluate = new SerializedAccessProperty<DateTime>();

        /// <summary>
        /// A flag to determine whether or not the pump should be running.
        /// </summary>
        public SerializedAccessProperty<bool> Running = new SerializedAccessProperty<bool>(true);

        /// <summary>
        /// A flag to determine whether or not the pump is stopped. This exists because there may be lag from when a 
        /// stop is requested to when it is actually finished stopping.
        /// </summary>
        public SerializedAccessProperty<bool> Stopped = new SerializedAccessProperty<bool>(false);

        /// <summary>
        /// Start the evaluation pump on a worker thread.
        /// </summary>
        public void Pump()
        {
            this.Running.Value = true;
            this.Stopped.Value = false;

            Action workerThreadDelegate = this.PumpInternal;
            //Invoke the worker on its own thread. When the thread finishes, call EndInvoke and mark the pump as stopped.
            workerThreadDelegate.BeginInvoke(new AsyncCallback(asyncResult =>
                {
                    ((Action)asyncResult.AsyncState).EndInvoke(asyncResult);
                    Stopped.Value = true;
                }), workerThreadDelegate);
        }

        /// <summary>
        /// The actual evaluation pump. Determine the next second that will occur and how long until it occurs.
        /// Sleep that long and evaluate the target evaluation second. Add one second to the target evaluation second,
        /// sleep until that second occurs, and repeat evaluation. 
        /// </summary>
        private void PumpInternal()
        {
            //The first time through, we need to set up the initial values.
            
            //Compute the floor of the current second.
            //There are 10,000,000 ticks per second. Subtract the remainder from now.
            DateTime utcNow = DateTime.UtcNow;
            DateTime utcNowFloor = new DateTime(utcNow.Ticks - (utcNow.Ticks % 10000000));
        
            //Compute the floor of the next second, and save it as the nextSecondToEvaluate
            NextSecondToEvaluate.Value = utcNowFloor.AddSeconds(1);

            //Begin the evaluation pump
            while (Running.Value)
            {
                //Determine how long from now it is until the nextSecondToEvaluate occurs.
                //In the general case, timeUntilNextEvaluation will be less than one second in the future.
                utcNow = DateTime.UtcNow;
                TimeSpan timeUntilNextEvaluation = NextSecondToEvaluate.Value - utcNow;

                //If the timeUntilNextEvaluation is positive, sleep that long, otherwise evaluate immediately.
                //This also serves as a preventative step so that we can catch up if we ever drift.
                if (timeUntilNextEvaluation > TimeSpan.Zero)
                {
                    Thread.Sleep(timeUntilNextEvaluation);
                }
                
                //TODO : use a stopwatch to capture how long the Evaluate method takes and publish a perf counter. "Percent time spent in evaluation."
                //If it gets close to a second, we're in trouble. 
                Evaluate(NextSecondToEvaluate.Value);

                NextSecondToEvaluate.Value = NextSecondToEvaluate.Value.AddSeconds(1);
            }

            //Do not signal here that the pump has stopped; that is handled by the Async operation.
        }

        /// <summary>
        /// Evaluate all of the rules in the schedule and see if they match the specified second.
        /// </summary>
        /// <returns>The number of schedules that evaluated to TRUE, that is, their conditions were met by this moment in time.</returns>
        private int Evaluate(DateTime secondToEvaluate)
        {
            //TODO : convert secondToEvaluate to a faster format and avoid the extra bit-shifts downstream.
            int i = 0;
            foreach (ScheduleDefinition scheduleItem in this.schedule.Values)
	        {
                i += scheduleItem.Evaluate(secondToEvaluate) ? 1 : 0;
	        }
            return i;
        }
    }
}
