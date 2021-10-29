# TaskSchedulerEngine

## Terminology

- Schedule Rule - cron-like rule, with second-level precision.
- Task - the thing to execute when schedule matches. The instance is shared by all executions forever and needs to be thread safe. If you need an instance per execution, make Task.OnScheduleRuleMatch a factory pattern.
- Schedule Rule Match - the current second matches a Schedule Rule so the Task should execute. A single Schedule Rule can only execute one Task. If you need to execute multiple tasks sequentially, initiate them from your Task. Multiple Schedule Rules that fire at the same time will execute in parallel (order not guaranteed).




## TODO

- ~~Simplify thread locking code~~
- ~~Make ITask an object instance and not created every time~~
- ~~Verify multiple tasks invoked by same scheuldeRule execute in parallel~~ - wrong, they are supposed to execute sequientially because they are += to schedule.ConditionsMet
- ~~ScheduleRule should only have a single Task to invoke. Make caller build their sequential logic into a Task. Simplifies multithreading understandability.~~
- ~~Remove TaskID (?) or replace it with Interlocked.Increment()~~ 
- Keep track of running tasks so they can gracefully shut down
- Use async/await pattern
- Use built-in Task class instead of ITask (?)
- Use strict mode to catch nulls 
- Use var in all definitions 
- Add year to support single execution
- Add expiration and on-start/on-stop methods. 
- Create "service host" that blocks and handles HUP/Kill/Restart events https://github.com/dotnet/runtime/issues/15178#issue-comment-box
- Remove singleton
- Fix bug where you call AddSchedule() before Start() and it throws null ref 