using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TaskSchedulerEngine.Configuration;
using System.Configuration;
using TaskSchedulerEngine.Fluent;

namespace TaskSchedulerEngine
{
    /// <summary>
    /// A worker thread wakes up at the start of every second and processes all 
    /// of the <see cref="Schedule"/> records.
    /// This class is a Singleton.
    /// </summary>
    /// <remarks>
    /// A <see cref="ReaderWriterLockSlim"/> is used to serialize access to the underlying _schedule Dictionary.
    /// This is necessary since evaluating the tasks happens on a background thread, while adding,
    /// removing and updating can happen on any thread.
    /// </remarks>
    internal class TaskEvaluationPump
    {
        /// <summary>
        /// Singleton to get the instance.
        /// </summary>
        public static TaskEvaluationPump GetInstance()
        {
            if(_instance == null)
            {
                lock (typeof(TaskEvaluationPump))
                {
                    if (_instance == null)
                    {
                        _instance = new TaskEvaluationPump();
                    }
                }
            }
            return _instance;
        }

        private static TaskEvaluationPump _instance;

        /// <summary>
        /// Used to serialize access to the _schedule.
        /// </summary>
        private static ReaderWriterLockSlim _scheduleLock = new ReaderWriterLockSlim();

        private Dictionary<string, ScheduleDefinition> _schedule { get; set; }

        /// <summary>
        /// Private constructor. Read from config and create ScheduleDefinitions from At objects, plus wire up delegates.
        /// </summary>
        private TaskEvaluationPump()
        {
        }

        /// <summary>
        /// Sets up the schedlue from configuration using the specified section, which must be <see cref="TaskSchedulerEngineConfigurationSection"/>.
        /// </summary>
        internal void InitializeFromConfig(string sectionName = "taskSchedulerEngine")
        {
            _scheduleLock.EnterWriteLock();

            _schedule = new Dictionary<string, ScheduleDefinition>();

            TaskSchedulerEngineConfigurationSection section = (TaskSchedulerEngineConfigurationSection)ConfigurationManager.GetSection(sectionName);

            //Loop over all of the tasks associated with that schedule.
            foreach (At at in section.Schedule)
            {
                //Create an evaluation-friendly schedule from the config-friendly schedule.
                ScheduleDefinition schedule = new ScheduleDefinition(at);
                foreach (var task in at.Execute)
                {
                    WireUpSchedule(schedule, Type.GetType(task.Type), task.Parameters);
                }
                _schedule.Add(at.Name, schedule);
            }

            _scheduleLock.ExitWriteLock();
        }

        internal void Initialize(IEnumerable<Schedule> fullSchedule)
        {
            _scheduleLock.EnterWriteLock();

            _schedule = new Dictionary<string, ScheduleDefinition>();

            foreach (Schedule sched in fullSchedule)
            {
                //Create an evaluation-friendly schedule from the config-friendly schedule.
                ScheduleDefinition schedule = new ScheduleDefinition(sched);
                
                //Loop over all of the tasks associated with that schedule.
                foreach (KeyValuePair<Type, object> task in sched.Tasks)
                {
                    WireUpSchedule(schedule, task.Key, task.Value);
                }
                _schedule.Add(sched.Name, schedule);
            }

            _scheduleLock.ExitWriteLock();
        }

        /// <summary>
        /// Hooks up the callbacks between a <see cref="ScheduleDefinition"/> and an <see cref="ITask"/>.
        /// </summary>
        /// <param name="schedule"></param>
        /// <param name="task"></param>
        /// <param name="parameters"></param>
        private void WireUpSchedule(ScheduleDefinition schedule, Type task, object parameters)
        {
            //Create an instance.
            object activated = Activator.CreateInstance(task);
            ITask itask = activated as ITask;
            if (itask == null)
            {
                throw new ArgumentException("Scheduled Tasks must be of Type ITask.");
            }

            //Wire up the delegate
            schedule.ConditionsMet += itask.HandleConditionsMetEvent;
            //Initialize the task.
            itask.Initialize(schedule, parameters);
            //And attach it to its schedule.
            schedule.Task = itask;
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
            // Make sure _schedule existed. It'll be null when user start without any schedule
            if (_schedule == null)
            {
                _schedule = new Dictionary<string, ScheduleDefinition>();
            }

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
            
            _scheduleLock.EnterReadLock();
            foreach (KeyValuePair<string, ScheduleDefinition> scheduleItem in _schedule)
            {
                i += scheduleItem.Value.Evaluate(secondToEvaluate) ? 1 : 0;
            }
            _scheduleLock.ExitReadLock();

            return i;
        }

        /// <summary>
        /// Gets the names of the running schedules.
        /// </summary>
        public IEnumerable<string> ListScheduleName()
        {
            _scheduleLock.EnterReadLock();
            var names = _schedule.Keys.ToArray();
            _scheduleLock.ExitWriteLock();

            return names;
        }

        /// <summary>
        /// Adds a schedule at runtime
        /// </summary>
        public bool AddSchedule(Schedule sched)
        {
            var added = false;
            if (sched != null)
            {
                ScheduleDefinition schedule = new ScheduleDefinition(sched);
                foreach (KeyValuePair<Type, object> task in sched.Tasks)
                {
                    WireUpSchedule(schedule, task.Key, task.Value);
                }


                _scheduleLock.EnterUpgradeableReadLock();
                if (!_schedule.ContainsKey(schedule.Name))
                {
                    _scheduleLock.EnterWriteLock();
                    if (!_schedule.ContainsKey(schedule.Name))
                        _schedule.Add(schedule.Name, schedule);
                    _scheduleLock.ExitWriteLock();
                    
                    added = true;
                }
                _scheduleLock.ExitUpgradeableReadLock();
            }

            return added;
        }

        /// <summary>
        /// Updates the schedule with a matching name.
        /// </summary>
        public bool UpdateSchedule(Schedule sched)
        {
            var updated = false;
            if (sched != null)
            {
                ScheduleDefinition schedule = new ScheduleDefinition(sched);
                foreach (KeyValuePair<Type, object> task in sched.Tasks)
                {
                    WireUpSchedule(schedule, task.Key, task.Value);
                }

                _scheduleLock.EnterUpgradeableReadLock();
                if (_schedule.ContainsKey(schedule.Name))
                {
                    _scheduleLock.EnterWriteLock();
                    if (_schedule.ContainsKey(schedule.Name))
                        _schedule[schedule.Name] = schedule;
                    _scheduleLock.ExitWriteLock();
                    updated = true;
                }
                _scheduleLock.ExitUpgradeableReadLock();
            }

            return updated;
        }

        /// <summary>
        /// Deletes the schedule specified by its name.
        /// </summary>
        /// <returns>True if deleted</returns>
        public bool DeleteSchedule(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            var deleted = false;
            _scheduleLock.EnterUpgradeableReadLock();
            if (_schedule.ContainsKey(name))
            {
                _scheduleLock.EnterWriteLock();
                if (_schedule.ContainsKey(name))
                    deleted = _schedule.Remove(name);
                _scheduleLock.ExitWriteLock();
            }
            _scheduleLock.ExitUpgradeableReadLock();
            
            return deleted;
        }
    }
}
