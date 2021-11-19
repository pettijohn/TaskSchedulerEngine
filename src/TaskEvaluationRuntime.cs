/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace TaskSchedulerEngine
{
    /// <summary>
    /// A worker thread wakes up at the start of every second and processes all 
    /// of the <see cref="ScheduleRule"/> records.
    /// This class is thread safe. 
    /// </summary>
    public class TaskEvaluationRuntime
    {
        /// <summary>
        /// Private constructor. Read from config and create ScheduleDefinitions from At objects, plus wire up delegates.
        /// </summary>
        public TaskEvaluationRuntime()
        {
            _runningTasks = new ConcurrentDictionary<Task, Task>();
            _schedule = new ConcurrentDictionary<string, ScheduleEvaluationOptimized>();
        }

        /// <summary>
        /// All of the schedules, keyed by Name, in a thread-safe Concurrent Dictionary
        /// </summary>
        private ConcurrentDictionary<string, ScheduleEvaluationOptimized> _schedule { get; set; }

        /// <summary>
        /// All currently running tasks. 
        /// </summary>
        private ConcurrentDictionary<Task, Task> _runningTasks;

        /// <summary>
        /// Hang onto the next second to evaluate. Thread-safe.
        /// </summary>
        private DateTime _nextSecondToEvaluate = DateTime.Now;
        private object _lock_nextSecondToEvaluate = new object();

        /// <summary>
        /// A flag to determine whether or not the pump is running.
        /// </summary>
        private TaskEvaluationRuntimeState _runState = TaskEvaluationRuntimeState.Stopped;
        private object _lock_runState = new object();

        public Action<Exception> HandleTaskException = (Exception e) => { Console.WriteLine(e); };

        private CancellationTokenSource? _evaluationLoopCancellationToken;

        /// <summary>
        /// Start the evaluation pump on a worker thread, waits for the thread to stop, then gracefully shuts down.
        /// Call RequestStop to stop the background thread. 
        /// </summary>
        public async Task RunAsync()
        {
            lock (_lock_runState)
            {
                if(_runState != TaskEvaluationRuntimeState.Stopped)
                    throw new InvalidOperationException("Can only start the service when stopped.");
                _runState = TaskEvaluationRuntimeState.Running;
            }

            //Invoke the worker on its own thread.
            var pumpTask = System.Threading.Tasks.Task.Run(this.EvaluationLoop);
            // Yield control and wait for PumpInteral's thread to end, signaled by RequestStop()
            await pumpTask;

            lock (_lock_runState)
            {
                _runState = TaskEvaluationRuntimeState.StoppingGracefully;
            }
            Console.WriteLine("Waiting for {0} Tasks to complete.", _runningTasks.Count);
            await Task.WhenAll(_runningTasks.Keys);
            lock (_lock_runState)
            {
                _runState = TaskEvaluationRuntimeState.Stopped;
            }
            Console.WriteLine("Stopped");
        }

        /// <summary>
        /// Sets a flag requesting the background thread to stop. 
        /// Returns immediately - you should already be awaiting on RunAsync. 
        /// </summary>
        /// <returns>True if stop requested successfully; false if stop already in progress or already stopped</returns>
        public bool RequestStop()
        {
            //Request that it stop running.
            lock (_lock_runState)
            {
                if (_runState == TaskEvaluationRuntimeState.Stopped 
                    || _runState == TaskEvaluationRuntimeState.StoppingGracefully
                    || _runState == TaskEvaluationRuntimeState.StopRequested)
                    return false;

                _runState = TaskEvaluationRuntimeState.StopRequested;
                if(_evaluationLoopCancellationToken != null)
                    _evaluationLoopCancellationToken.Cancel();
                return true;
            }
        }


        /// <summary>
        /// The actual evaluation loop. Determine the next second that will occur and how long until it occurs.
        /// Sleep that long and evaluate the target evaluation second. Add one second to the target evaluation second,
        /// sleep until that second occurs, and repeat evaluation. 
        /// </summary>
        private async Task EvaluationLoop()
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

            //Set up a cancellation token for the main loop
            using (_evaluationLoopCancellationToken = new CancellationTokenSource())
            {
                //Begin the evaluation pump
                while (!_evaluationLoopCancellationToken.IsCancellationRequested)
                {
                    lock (_lock_runState)
                    {
                        if (_runState != TaskEvaluationRuntimeState.Running)
                        {
                            break;
                        }
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
                        // Can I await Thread.Delay with cancellation token, and cancel immediately when StopRequest? 
                        // https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.delay?view=net-6.0#System_Threading_Tasks_Task_Delay_System_Int32_System_Threading_CancellationToken_
                        try
                        {
                            await Task.Delay(timeUntilNextEvaluation, _evaluationLoopCancellationToken.Token);
                        }
                        catch (OperationCanceledException _)
                        {
                            break;
                        }
                        //Thread.Sleep(timeUntilNextEvaluation);
                    }

                    //TODO : use a stopwatch to capture how long the Evaluate method takes and publish a perf counter. "Percent time spent in evaluation."
                    //If it gets close to a second, we're in trouble. 
                    Evaluate(_nextSecondToEvaluate);

                    lock (_lock_nextSecondToEvaluate)
                    {
                        _nextSecondToEvaluate = _nextSecondToEvaluate.AddSeconds(1);
                    }
                }
            }
        }

        /// <summary>
        /// Evaluate all of the rules in the schedule and see if they match the specified second. If it matches, spin it off on a new thread. 
        /// </summary>
        /// <returns>The number of schedules that evaluated to TRUE, that is, their conditions were met by this moment in time.</returns>
        private int Evaluate(DateTime secondToEvaluate)
        {
            //TODO : convert secondToEvaluate to a faster format and avoid the extra bit-shifts downstream.
            int i = 0;
            
            foreach (KeyValuePair<string, ScheduleEvaluationOptimized> scheduleItem in _schedule)
            {
                var eventArgs = scheduleItem.Value.Evaluate(secondToEvaluate);
                if(eventArgs != null)
                {
                    i++;
                    var workerTask = System.Threading.Tasks.Task.Run(() => scheduleItem.Value.Task.OnScheduleRuleMatch(eventArgs, _evaluationLoopCancellationToken.Token));
                    // TODO - exception handling of Task threads 
                    // Keep a ConcurrentDict of running Tasks for graceful shutdown 
                    _runningTasks[workerTask] = workerTask;
                    workerTask.ContinueWith(t =>
                    {
                        Console.WriteLine("Running task count: " + _runningTasks.Count);
                        _runningTasks.Remove(t, out t);
                    });
                }
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
                added = _schedule.TryAdd(schedule.Name, schedule);
            }

            return added;
        }

        /// <summary>
        /// Unconditionally update (or add) the schedule with a matching name.
        /// </summary>
        public void UpdateSchedule(ScheduleRule sched)
        {
            if (sched != null)
            {
                ScheduleEvaluationOptimized schedule = new ScheduleEvaluationOptimized(sched);
                _schedule[schedule.Name] = schedule;
            }
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
