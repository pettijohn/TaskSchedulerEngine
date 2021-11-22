using System;
using System.Threading;
using TaskSchedulerEngine;

/// <summary>
/// Internal helper class to wire up an Action<> to an IScheduledTask. 
/// </summary>
internal class AnonymousScheduledTask : IScheduledTask
{
    public AnonymousScheduledTask(Action<ScheduleRuleMatchEventArgs, CancellationToken> callback)
    {
        Callback = callback;
    }

    public Action<ScheduleRuleMatchEventArgs, CancellationToken> Callback { get; set; }

    public void OnScheduleRuleMatch(ScheduleRuleMatchEventArgs e, CancellationToken c)
    {
        Callback(e, c);
    }
}