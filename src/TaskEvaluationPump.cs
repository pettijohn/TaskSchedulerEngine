/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TaskSchedulerEngine
{
    /// <summary>
    /// A worker thread wakes up at the start of every second and processes all 
    /// of the <see cref="ScheduleRule"/> records.
    /// This class is a Singleton and is thread safe. 
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
        /// All of the schedules, keyed by Name, in a thread-safe Concurrent Dictionary
        /// </summary>
        private ConcurrentDictionary<string, ScheduleEvaluationOptimized> _schedule { get; set; }

        /// <summary>
        /// Private constructor. Read from config and create ScheduleDefinitions from At objects, plus wire up delegates.
        /// </summary>
        private TaskEvaluationPump()
        {
        }

        internal void Initialize(IEnumerable<ScheduleRule> fullSchedule)
        {
            _schedule = new ConcurrentDictionary<string, ScheduleEvaluationOptimized>();

            foreach (ScheduleRule sched in fullSchedule)
            {
                //Create an evaluation-friendly schedule from the config-friendly schedule.
                ScheduleEvaluationOptimized schedule = new ScheduleEvaluationOptimized(sched);
                
                //Loop over all of the tasks associated with that schedule.
                foreach (ITask task in sched.Tasks)
                {
                    WireUpSchedule(schedule, task);
                }
                _schedule[sched.Name] = schedule;
            }
        }

        /// <summary>
        /// Hooks up the callbacks between a <see cref="ScheduleEvaluationOptimized"/> and an <see cref="ITask"/>.
        /// </summary>
        /// <param name="schedule"></param>
        /// <param name="task"></param>
        /// <param name="parameters"></param>
        private void WireUpSchedule(ScheduleEvaluationOptimized schedule, ITask taskInstance)
        {
            if (taskInstance == null)
            {
                throw new ArgumentNullException("taskInstance must not be null.");
            }

            //Wire up the delegate
            schedule.ConditionsMet = taskInstance.OnScheduleRuleMatch;

            //And attach it to its schedule.
            schedule.Task = taskInstance;
        }

        /// <summary>
        /// Hang onto the next second to evaluate. Thread-safe.
        /// </summary>
        private DateTime _nextSecondToEvaluate = DateTime.Now;
        private object _lock_nextSecondToEvaluate = new object();

        /// <summary>
        /// A flag to determine whether or not the pump is running.
        /// </summary>
        private TaskPumpRunState _runState = TaskPumpRunState.Stopped;
        private object _lock_runState = new object();

        /// <summary>
        /// Start the evaluation pump on a worker thread.
        /// </summary>
        public void Pump()
        {
            // Make sure _schedule existed. It'll be null when user start without any schedule
            if (_schedule == null)
            {
                _schedule = new ConcurrentDictionary<string, ScheduleEvaluationOptimized>();
            }

            lock (_lock_runState)
            {
                _runState = TaskPumpRunState.Running;
            }

            Action workerThreadDelegate = this.PumpInternal;
            //Invoke the worker on its own thread. When the thread finishes, call EndInvoke and mark the pump as stopped.
            var pumpTask = System.Threading.Tasks.Task.Run(() => workerThreadDelegate.Invoke());
            pumpTask.ContinueWith(t =>
            {
                lock (_lock_runState)
                {
                    _runState = TaskPumpRunState.Stopped;
                }
            });
            // workerThreadDelegate.BeginInvoke(new AsyncCallback(asyncResult =>
            //     {
            //         ((Action)asyncResult.AsyncState).EndInvoke(asyncResult);
            //         RunState.Value = TaskPumpRunState.Stopped;
            //     }), workerThreadDelegate);
        }

        public void Stop()
        {
            //Request that it stop running.
            lock (_lock_runState)
            {
                _runState = TaskPumpRunState.Stopping;
            }

            //Wait for it to stop running.
            while (true)
            {
                lock(_lock_runState)
                {
                    if (_runState == TaskPumpRunState.Stopped)
                        break;
                }
                Thread.Sleep(10);
            }
        }

        /// <summary>
        /// The actual evaluation pump. Determine the next second that will occur and how long until it occurs.
        /// Sleep that long and evaluate the target evaluation second. Add one second to the target evaluation second,
        /// sleep until that second occurs, and repeat evaluation. 
        /// </summary>
        private void PumpInternal()
        {
            Console.WriteLine("Pump Internal on Thread " + System.Threading.Thread.CurrentThread.ManagedThreadId);
            //The first time through, we need to set up the initial values.

            //Compute the floor of the current second.
            //There are 10,000,000 ticks per second. Subtract the remainder from now.
            DateTime utcNow = DateTime.UtcNow;
            DateTime utcNowFloor = new DateTime(utcNow.Ticks - (utcNow.Ticks % 10000000));

            //Compute the floor of the next second, and save it as the nextSecondToEvaluate
            lock (_lock_nextSecondToEvaluate)
            {
                _nextSecondToEvaluate = utcNowFloor.AddSeconds(1);
            }

            //Begin the evaluation pump
            while (true)
            {
                lock(_lock_runState)
                {
                    if (_runState != TaskPumpRunState.Running)
                        break;
                }

                TimeSpan timeUntilNextEvaluation = TimeSpan.Zero;
                //Determine how long from now it is until the nextSecondToEvaluate occurs.
                //In the general case, timeUntilNextEvaluation will be less than one second in the future.
                lock (_lock_nextSecondToEvaluate)
                {
                    utcNow = DateTime.UtcNow;
                    timeUntilNextEvaluation = _nextSecondToEvaluate - utcNow;
                }

                //If the timeUntilNextEvaluation is positive, sleep that long, otherwise evaluate immediately.
                //This also serves as a preventative step so that we can catch up if we ever drift.
                if (timeUntilNextEvaluation > TimeSpan.Zero)
                {
                    Thread.Sleep(timeUntilNextEvaluation);
                }
                
                //TODO : use a stopwatch to capture how long the Evaluate method takes and publish a perf counter. "Percent time spent in evaluation."
                //If it gets close to a second, we're in trouble. 
                Evaluate(_nextSecondToEvaluate);

                lock (_lock_nextSecondToEvaluate)
                {
                    _nextSecondToEvaluate = _nextSecondToEvaluate.AddSeconds(1);
                }
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
            
            foreach (KeyValuePair<string, ScheduleEvaluationOptimized> scheduleItem in _schedule)
            {
                i += scheduleItem.Value.Evaluate(secondToEvaluate) ? 1 : 0;
            }

            return i;
        }

        /// <summary>
        /// Gets the names of the running schedules.
        /// </summary>
        public IEnumerable<string> ListScheduleName()
        {
            var names = _schedule.Keys.ToArray();

            return names;
        }

        /// <summary>
        /// Adds a schedule at runtime
        /// </summary>
        public bool AddSchedule(ScheduleRule sched)
        {
            var added = false;
            if (sched != null)
            {
                ScheduleEvaluationOptimized schedule = new ScheduleEvaluationOptimized(sched);
                foreach (ITask task in sched.Tasks)
                {
                    WireUpSchedule(schedule, task);
                }

                added = _schedule.TryAdd(schedule.Name, schedule);
            }

            return added;
        }

        /// <summary>
        /// Updates the schedule with a matching name.
        /// </summary>
        public bool UpdateSchedule(ScheduleRule sched)
        {
            var updated = false;
            if (sched != null)
            {
                ScheduleEvaluationOptimized schedule = new ScheduleEvaluationOptimized(sched);
                foreach (ITask task in sched.Tasks)
                {
                    WireUpSchedule(schedule, task);
                }

                // If there is a matching schedule name, get it
                ScheduleEvaluationOptimized oldValue = null;
                if(!_schedule.TryGetValue(sched.Name, out oldValue))
                    return false;

                // And if it is still there with th old value, update it
                updated = _schedule.TryUpdate(schedule.Name, schedule, oldValue);
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
            ScheduleEvaluationOptimized removed = null;
            deleted = _schedule.TryRemove(name, out removed);
            
            return deleted;
        }
    }
}
