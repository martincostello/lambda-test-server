# Coding Agent Instructions

This file provides guidance to coding agents when working with code in this repository.

## Build, test, and lint commands

- Prefer `./build.ps1` from the repository root. It bootstraps the exact SDK from `global.json`, packs `src\AwsLambdaTestServer`, and runs the main test project plus the sample test projects.
- Build only: `dotnet build ./AwsLambdaTestServer.slnx -c Release`
- Run the main test project: `dotnet test ./tests/AwsLambdaTestServer.Tests/MartinCostello.Testing.AwsLambdaTestServer.Tests.csproj -c Release`
- Run a sample test project: `dotnet test ./samples/MathsFunctions.Tests/MathsFunctions.Tests.csproj -c Release`
- Run a single test from the main test project: `dotnet test ./tests/AwsLambdaTestServer.Tests/MartinCostello.Testing.AwsLambdaTestServer.Tests.csproj -c Release -p:CollectCoverage=false --filter "DisplayName=Function_Reverses_Numbers"`
- List tests in the main test project: `dotnet test ./tests/AwsLambdaTestServer.Tests/MartinCostello.Testing.AwsLambdaTestServer.Tests.csproj -c Release -p:CollectCoverage=false --list-tests`
- There is no single local lint script. CI linting is defined in `.github\workflows\lint.yml` and runs:
  - `actionlint` for GitHub Actions workflows
  - `zizmor` for workflow security linting
  - `markdownlint-cli2` for Markdown
  - `Invoke-ScriptAnalyzer -Path <repo> -Recurse -ReportSummary -Settings @{ IncludeDefaultRules = $true; Severity = @('Error', 'Warning') }` for PowerShell

## High-level architecture

- The package is an in-memory AWS Lambda runtime emulator for tests. `LambdaTestServer` starts a minimal ASP.NET Core app and exposes the Lambda Runtime API routes that `Amazon.Lambda.RuntimeSupport.LambdaBootstrap` expects.
- `LambdaTestServer.StartAsync()` does more than start a host:
  - it creates a `RuntimeHandler`
  - it starts a minimal host with `UseTestServer()` by default
  - it sets process-level Lambda environment variables such as `AWS_LAMBDA_RUNTIME_API`, function metadata, and optional memory-limit overrides
  - it reloads configuration so the started app sees those environment variables
- `RuntimeHandler` is the core coordination layer. It keeps an unbounded channel of `LambdaTestRequest` items, dequeues them when the Lambda runtime calls `GET /{LambdaVersion}/runtime/invocation/next`, and correlates the eventual `/response` or `/error` POST back to the original request by AWS request id.
- Each enqueued request returns a `LambdaTestContext` whose `Response` is a channel reader. Tests enqueue input first, then run the Lambda entrypoint against `server.CreateClient()`, then read the response from that channel.
- The default server is fully in-memory, but tests also exercise a real loopback HTTP server path through `HttpLambdaTestServer`, which overrides the web host to use Kestrel instead of `TestServer`.
- The solution layout is deliberate:
  - `src\AwsLambdaTestServer` contains the reusable package
  - `tests\AwsLambdaTestServer.Tests` contains library tests plus executable examples of the intended testing pattern
  - `samples\*` and `samples\*.Tests` show real Lambda functions and how the library is expected to be consumed

## Key conventions

- Lambda entrypoints used with this library are factored into a `RunAsync(HttpClient? httpClient = null, CancellationToken cancellationToken = default)` shape. Tests inject `server.CreateClient()` into `LambdaBootstrap` through that method instead of invoking opaque `Main()` logic.
- Tests usually stop the Lambda bootstrap loop by linking a timeout token with `TestContext.Current.CancellationToken` and setting `server.OnInvocationCompleted` to cancel the shutdown token after the queued invocation completes.
- Do not assume tests are safe to parallelize. The test assembly disables collection parallelization, and `LambdaTestServer` mutates process-wide Lambda environment variables and memory-limit state.
- The main test project enforces coverage thresholds in the project file. For single-test runs or `--list-tests`, disable coverage with `-p:CollectCoverage=false` or the run can fail even when discovery succeeds.
- Public API changes need a matching update under `src\AwsLambdaTestServer\PublicAPI\` because the repository uses `Microsoft.CodeAnalysis.PublicApiAnalyzers`.
- Style is enforced by repository-level configuration rather than ad hoc formatting:
  - C# files use file-scoped namespaces and the standard Apache license header
  - public and internal API surface is expected to stay XML-documented
  - JSON, YAML, `.props`, and `.csproj` files use 2-space indentation

## General guidelines

- Always ensure code compiles with no warnings or errors and tests pass locally before pushing changes.
- Do not change the public API unless specifically requested.
- Do not use APIs marked with `[Obsolete]`.
- Bug fixes should **always** include a test that would fail without the corresponding fix.
- Do not introduce new dependencies unless specifically requested.
- Do not update existing dependencies unless specifically requested.
