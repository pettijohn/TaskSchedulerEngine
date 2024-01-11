/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */
using System;
using System.Threading;
using System.Threading.Tasks;
using TaskSchedulerEngine;

/// <summary>
/// Internal helper class to wire up an Action<> to an IScheduledTask. 
/// </summary>
internal class AnonymousScheduledTask : IScheduledTask
{
    public AnonymousScheduledTask(Func<ScheduleRuleMatchEventArgs, CancellationToken, Task<bool>> callback)
    {
        Callback = callback;
    }

    public Func<ScheduleRuleMatchEventArgs, CancellationToken, Task<bool>> Callback { get; set; }

    public Task<bool> OnScheduleRuleMatch(ScheduleRuleMatchEventArgs e, CancellationToken c)
    {
        return Callback(e, c);
    }
}