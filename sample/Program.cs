/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */

using sample;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
