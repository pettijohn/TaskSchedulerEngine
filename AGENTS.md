# AGENTS.md

## Project Context
- **Tech Stack:** C# with nullable reference types; SDK-style .NET library targeting .NET 8.0, .NET 9.0, and .NET 10.0; .NET 8/9/10 Worker Service sample using Microsoft.Extensions.Hosting 10.0.0; MSTest 4.0.2 with Microsoft.NET.Test.Sdk 18.0.1 and Coverlet 6.0.4; no database or external runtime dependency.
- **Core Purpose:** This repository builds a lightweight, in-memory, cron-like task scheduler with second-level precision, timezone-aware rules, asynchronous execution, graceful shutdown, and exponential-backoff retries. Schedule rules are expressed through a fluent C# API or restricted five-field cron expressions and compiled into bitfields for fast per-second evaluation.

## Verification & Build Commands
- **Install Dependencies:** `dotnet restore TaskSchedulerEngine.sln`
- **Development Server:** `dotnet run --project sample/sample.csproj --framework net10.0`
- **Run Tests:** `DOTNET_ROLL_FORWARD=Major dotnet test test/TaskSchedulerEngineTests.csproj`
- **Lint & Format:** `dotnet format TaskSchedulerEngine.sln --verify-no-changes --no-restore`

## Repository Architecture & Map
- `src/TaskSchedulerEngine.csproj` and `src/*.cs` — zero-package library and public API; `ScheduleRule` builds and live-updates rules, while `IScheduledTask` defines the callback contract.
- `src/ScheduleEvaluationOptimized.cs` — converts rule fields into 64-bit masks and evaluates UTC instants after timezone conversion; empty arrays represent wildcards.
- `src/TaskEvaluationRuntime.cs` — thread-safe, once-per-second evaluation pump; owns schedules, dispatches matching callbacks, tracks running tasks, reports task faults, and coordinates graceful shutdown.
- `src/ExponentialBackoffTask.cs`, `src/RetryableTaskArgs.cs`, and `src/RetryableTaskInvocation.cs` — retry adapter that schedules one-shot follow-up rules at exponentially increasing intervals.
- `sample/` and `test/` — .NET 8/9/10 hosted-worker usage example and MSTest suite covering rule parsing/evaluation, threading, shutdown, expiration, timezones, and retries.

## Coding Style & Development Norms
- Follow the existing C# layout: four-space indentation, braces on new lines, PascalCase public members/types, camelCase locals and parameters, `_camelCase` private fields, XML documentation on public scheduling behavior, and nullable annotations. There is no `.editorconfig` or analyzer package; avoid repository-wide formatting churn and keep the library dependency-free unless explicitly approved.
- Validate invalid rule and retry inputs at the API boundary with `ArgumentException`, `ArgumentOutOfRangeException`, `ArgumentNullException`, or `InvalidOperationException`. Scheduled callbacks should honor the supplied `CancellationToken`, catch expected operational failures and return `false` when retry behavior is desired; unexpected callback faults are surfaced through `TaskEvaluationRuntime.UnhandledScheduledTaskException` and must not terminate the evaluation loop.
- Tests use MSTest classes marked `[TestClass]` and focused PascalCase methods marked `[TestMethod]`, conventionally ending in `Test`. Add coverage for valid and invalid boundaries, wildcard behavior, timezone conversion, and async/concurrency effects; use UTC/future-relative dates and explicit timing margins for second-boundary tests, then stop and await every started runtime.

## Critical Constraints ("Landmines")
- Preserve compatibility across `net8.0`, `net9.0`, and `net10.0`; APIs such as source-generated regex require conditional compilation. The devcontainer installs only .NET SDK/runtime 10, so `DOTNET_ROLL_FORWARD=Major` is required when running all test target frameworks there.
- An unset or empty schedule field means wildcard/every value, so omitting `AtSeconds(0)` can make a rule fire every second. `FromCron` supports only five fields, `*`, and comma-separated integers; it deliberately rejects ranges and steps and always initializes seconds to zero.
- `ScheduleRule.MinYear` is computed as the current local year minus one and the bitfield supports only 63 year offsets. Keep tests relative to `MinYear` rather than fixed historical years, and use custom `TimeZoneInfo` instances in unit tests so they do not depend on host tzdata.
- A rule is registered only after `Execute`/`ExecuteAndRetry` attaches a task; subsequent fluent mutations immediately replace its optimized snapshot. `AsActive(false)` removes it, and one-shot/retry rules need expiration semantics to avoid retaining schedules indefinitely.
- A single `IScheduledTask` instance may be invoked concurrently and must be thread-safe. Preserve the concurrent dictionaries, locking, `Interlocked` task IDs, independent callback dispatch, cancellation flow, and graceful wait for all running tasks when changing runtime lifecycle code.
- The formatter verification currently reports pre-existing whitespace and CA2017 issues, so do not mass-format unrelated files while making focused changes. Treat `pack.ps1` as a release operation: it mutates `PackageVersion`, reads the private `.nuget` key, publishes a package, commits, tags, and pushes; never run it or change package metadata without explicit permission.
