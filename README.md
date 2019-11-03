# AWS Lambda Test Server for .NET Core

[![NuGet](https://buildstats.info/nuget/MartinCostello.Testing.AwsLambdaTestServer?includePreReleases=true)](http://www.nuget.org/packages/MartinCostello.Testing.AwsLambdaTestServer "Download MartinCostello.Testing.AwsLambdaTestServer from NuGet")

| | Windows | Linux/macOS | Linux/macOS/Windows |
|:-:|:-:|:-:|:-:|
| **Build Status** | [![Windows build status](https://img.shields.io/appveyor/ci/martincostello/lambda-test-server/master.svg)](https://ci.appveyor.com/project/martincostello/lambda-test-server) [![Code coverage](https://codecov.io/gh/martincostello/lambda-test-server/branch/master/graph/badge.svg)](https://codecov.io/gh/martincostello/lambda-test-server) | [![Linux/macOS build status](https://img.shields.io/travis/com/martincostello/lambda-test-server/master.svg)](https://travis-ci.com/martincostello/lambda-test-server) | [![Azure Pipelines build status](https://martincostello.visualstudio.com/lambda-test-server/_apis/build/status/martincostello.lambda-test-server?branchName=master)](https://dev.azure.com/martincostello/lambda-test-server/_build/latest?definitionId=74&branchName=master) |
| **Build History** | [![Windows build history](https://buildstats.info/appveyor/chart/martincostello/lambda-test-server?branch=master&includeBuildsFromPullRequest=false)](https://ci.appveyor.com/project/martincostello/lambda-test-server) | [![Linux build history](https://buildstats.info/travisci/chart/martincostello/lambda-test-server?branch=master&includeBuildsFromPullRequest=false)](https://travis-ci.com/martincostello/lambda-test-server) | [![Build history](https://buildstats.info/azurepipelines/chart/martincostello/lambda-test-server/74?branch=master&includeBuildsFromPullRequest=false)](https://dev.azure.com/martincostello/lambda-test-server/_build?definitionId=74) |

## Introduction

A NuGet package that builds on top of the `TestServer` class in the [Microsoft.AspNetCore.TestHost](https://www.nuget.org/packages/Microsoft.AspNetCore.TestHost) NuGet package to provide infrastructure to use with end-to-end/integration tests of .NET Core 3.0 AWS Lambda Functions using a custom runtime with the `LambdaBootstrap` class from the [Amazon.Lambda.RuntimeSupport](https://www.nuget.org/packages/Amazon.Lambda.RuntimeSupport/) NuGet package.

[_.NET Core 3.0 on Lambda with AWS Lambda’s Custom Runtime_](https://aws.amazon.com/blogs/developer/net-core-3-0-on-lambda-with-aws-lambdas-custom-runtime/ ".NET Core 3.0 on Lambda with AWS Lambda’s Custom Runtime on the AWS Developer Blog")

### Installation

To install the library from [NuGet](https://www.nuget.org/packages/MartinCostello.Testing.AwsLambdaTestServer/ "MartinCostello.Testing.AwsLambdaTestServer on NuGet.org") using the .NET SDK run:

```
dotnet add package MartinCostello.Testing.AwsLambdaTestServer
```

### Usage

Before you can use the Lambda test server to test your function, you need to factor your function entry-point
in such a way that you can supply both a `HttpClient` and `CancellationToken` to it from your tests. This is to allow you to both plug in the `HttpClient` for the test server into `LambdaBootstrap`, and to stop the Lambda function running at a time of your choosing by signalling the `CancellationToken`.

Here's an example of how to do this with a simple Lambda function that takes an array of integers and returns them in reverse order:

```csharp
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.Json;

namespace MyFunctions
{
    public static class ReverseFunction
    {
        public static async Task Main()
            => await RunAsync();

        public static async Task RunAsync(
            HttpClient httpClient = null,
            CancellationToken cancellationToken = default)
        {
            var serializer = new JsonSerializer();

            using var handlerWrapper = HandlerWrapper.GetHandlerWrapper<int[], int[]>(ReverseAsync, serializer);
            using var bootstrap = new LambdaBootstrap(handlerWrapper);

            if (httpClient != null)
            {
                // Use reflection to assign the HttpClient to the LambdaBootstrap instance
                var client = new RuntimeApiClient(httpClient);
                var type = bootstrap.GetType();
                var property = type.GetProperty("Client", BindingFlags.Instance | BindingFlags.NonPublic);

                property.SetValue(bootstrap, client);
            }

            await bootstrap.RunAsync(cancellationToken);
        }

        public static Task<int[]> ReverseAsync(int[] values)
        {
            return Task.FromResult(values.Reverse().ToArray());
        }
    }
}
```

Notice the use of reflection to set a new `RuntimeApiClient` instance using the specified `HttpClient` value onto the created instance of `LambdaBootstrap`. At the time of writing, this is the only way to configure things to use the Lambda test server to process requests.

> I've reached out to the AWS Lambda .NET team with a suggestion to provide a supported way to specify a custom `HttpClient` in a future version of the NuGet package here: https://github.com/aws/aws-lambda-dotnet/pull/540

Once you've done that, you can use `LambdaTestServer` in your tests with your function to verify how it processes requests.

Here's an example using xunit to verify that `ReverseFunction` works as intended:

```csharp
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MartinCostello.Testing.AwsLambdaTestServer;
using Newtonsoft.Json;
using Xunit;

namespace MyFunctions
{
    public static class ReverseFunctionTests
    {
        [Fact]
        public static async Task Function_Reverses_Numbers()
        {
            // Arrange
            using var server = new LambdaTestServer();
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            await server.StartAsync(cancellationTokenSource.Token);

            int[] value = new[] { 1, 2, 3 };
            string json = JsonConvert.SerializeObject(value);

            LambdaTestContext context = await server.EnqueueAsync(json);

            using var httpClient = server.CreateClient();

            // Act
            await ReverseFunction.RunAsync(httpClient, cancellationTokenSource.Token);

            // Assert
            Assert.True(context.Response.TryRead(out LambdaTestResponse response));
            Assert.NotNull(response);
            Assert.True(response.IsSuccessful);
            Assert.NotNull(response.Content);

            json = Encoding.UTF8.GetString(response.Content);
            int[] actual = JsonConvert.DeserializeObject<int[]>(json);

            Assert.Equal(new[] { 3, 2, 1 }, actual);
        }
    }
}
```

The key parts to call out here are:

  1. An instance of `LambdaTestServer` is created and then the `StartAsync()` method called with a `CancellationToken` that allows the test to stop the function. In the example here the token is signalled with a timeout, but you could also write code to stop the processing based on arbitrary criteria.
  1. A request that the Lambda function should be invoked with is passed to `EnqueueAsync()`. This can be specified with an instance of `LambdaTestRequest` for fine-grained control, but there are overloads that accept `byte[]` and `string`. You could also make your own extensions to serialize objects to JSON using the serializer of your choice.
  1. `EnqueueAsync()` returns a `LambdaTestContext`. This contains a reference to the `LambdaTestRequest` and a `ChannelReader<LambdaTestResponse>`. This channel reader can be used to await the request being processed by the function under test.
  1. Once the request is enqueued, an `HttpClient` is obtained from the test server and passed to the function to test to run it.
  1. Once the function processing completes when the `CancellationToken` is signalled, the channel reader is read to obtain the `LambdaTestResponse` for the request that was enqueued.
  1. Once this is returned, the response is checked for success using `IsSuccessful` and then the `Content` (which is a `byte[]`) is deserialized into the expected response to be asserted on. Again, you could make your own extensions to deserialize the response content into `string` or objects from JSON.

You can find more examples in the [unit tests](https://github.com/martincostello/lambda-test-server/blob/master/tests/AwsLambdaTestServer.Tests/Examples.cs "Unit test examples").

### Advanced Usage

#### Lambda Runtime Options

#### Logging from the Test Server

To help diagnose failing tests, the `LambdaTestServer` outputs logs of the requests it receives to the emulated AWS Lambda Runtime it provides. To route the logging output to a location of your choosing, you can use the configuration callbacks, such as the constructor overload that accepts an `Action<IServiceCollection>` or the `Configure` property on the `LambdaTestServerOptions` class.

Here's an example of configuring the test server to route its logs to xunit using the [xunit-logging](https://www.nuget.org/packages/MartinCostello.Logging.XUnit) library:

```csharp
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MartinCostello.Logging.XUnit;
using MartinCostello.Testing.AwsLambdaTestServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace MartinCostello.Testing.AwsLambdaTestServer
{
    public class ReverseFunctionWithLoggingTests : ITestOutputHelperAccessor
    {
        public ReverseFunctionWithLoggingTests(ITestOutputHelper outputHelper)
        {
            OutputHelper = outputHelper;
        }

        public ITestOutputHelper OutputHelper { get; set; }

        [Fact]
        public async Task Function_Reverses_Numbers()
        {
            // Arrange
            using var server = new LambdaTestServer(
                (services) => services.AddLogging(
                    (builder) => builder.AddXUnit(this)));

            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            await server.StartAsync(cancellationTokenSource.Token);

            int[] value = new[] { 1, 2, 3 };
            string json = JsonConvert.SerializeObject(value);

            LambdaTestContext context = await server.EnqueueAsync(json);

            using var httpClient = server.CreateClient();

            // Act
            await ReverseFunction.RunAsync(httpClient, cancellationTokenSource.Token);

            // Assert
            Assert.True(context.Response.TryRead(out LambdaTestResponse response));
            Assert.NotNull(response);
            Assert.True(response.IsSuccessful);
            Assert.NotNull(response.Content);

            json = Encoding.UTF8.GetString(response.Content);
            int[] actual = JsonConvert.DeserializeObject<int[]>(json);

            Assert.Equal(new[] { 3, 2, 1 }, actual);
        }
    }
}
```

This then outputs logs similar to the below into the xunit test results:

```
Test Name:	Function_Reverses_Numbers
Test Outcome:	Passed
Result StandardOutput:
[2019-11-03 18:35:28Z] info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost/2018-06-01/runtime/invocation/next  
[2019-11-03 18:35:28Z] info: Microsoft.AspNetCore.Routing.EndpointMiddleware[0]
      Executing endpoint '/{LambdaVersion}/runtime/invocation/next HTTP: GET'
[2019-11-03 18:35:28Z] info: MartinCostello.Testing.AwsLambdaTestServer.RuntimeHandler[0]
      Waiting for new request for Lambda function with ARN arn:aws:lambda:eu-west-1:123456789012:function:test-function.
[2019-11-03 18:35:28Z] info: MartinCostello.Testing.AwsLambdaTestServer.RuntimeHandler[0]
      Invoking Lambda function with ARN arn:aws:lambda:eu-west-1:123456789012:function:test-function for request Id 0e42d2be-2400-4fc6-a75f-1cc33a91dab3 and trace Id eff856e7-b1e3-4d97-99fb-7686b69b3bc4.
[2019-11-03 18:35:28Z] info: Microsoft.AspNetCore.Routing.EndpointMiddleware[1]
      Executed endpoint '/{LambdaVersion}/runtime/invocation/next HTTP: GET'
[2019-11-03 18:35:28Z] info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished in 51.5962ms 200 application/json
[2019-11-03 18:35:28Z] info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost/2018-06-01/runtime/invocation/0e42d2be-2400-4fc6-a75f-1cc33a91dab3/response application/json
[2019-11-03 18:35:28Z] info: Microsoft.AspNetCore.Routing.EndpointMiddleware[0]
      Executing endpoint '/{LambdaVersion}/runtime/invocation/{AwsRequestId}/response HTTP: POST'
[2019-11-03 18:35:28Z] info: MartinCostello.Testing.AwsLambdaTestServer.RuntimeHandler[0]
      Invoked Lambda function with ARN arn:aws:lambda:eu-west-1:123456789012:function:test-function for request Id 0e42d2be-2400-4fc6-a75f-1cc33a91dab3: [3,2,1].
[2019-11-03 18:35:28Z] info: Microsoft.AspNetCore.Routing.EndpointMiddleware[1]
      Executed endpoint '/{LambdaVersion}/runtime/invocation/{AwsRequestId}/response HTTP: POST'
[2019-11-03 18:35:28Z] info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished in 20.2114ms 204
```

## Feedback

Any feedback or issues can be added to the issues for this project in [GitHub](https://github.com/martincostello/lambda-test-server/issues "Issues for this project on GitHub.com").

## Repository

The repository is hosted in [GitHub](https://github.com/martincostello/lambda-test-server "This project on GitHub.com"): https://github.com/martincostello/lambda-test-server.git

## License

This project is licensed under the [Apache 2.0](http://www.apache.org/licenses/LICENSE-2.0.txt "The Apache 2.0 license") license.

## Building and Testing

Compiling the library yourself requires Git and the [.NET Core SDK](https://www.microsoft.com/net/download/core "Download the .NET Core SDK") to be installed (version `3.0.100` or later).

To build and test the library locally from a terminal/command-line, run one of the following set of commands:

**Windows**

```powershell
git clone https://github.com/martincostello/lambda-test-server.git
cd lambda-test-server
.\Build.ps1
```

**Linux/macOS**

```sh
git clone https://github.com/martincostello/lambda-test-server.git
cd lambda-test-server
./build.sh
```
