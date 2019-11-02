# AWS Lambda Test Server for .NET Core

[![NuGet](https://buildstats.info/nuget/MartinCostello.Testing.AwsLambdaTestServer?includePreReleases=true)](http://www.nuget.org/packages/MartinCostello.Testing.AwsLambdaTestServer "Download MartinCostello.Testing.AwsLambdaTestServer from NuGet")

| | Windows | Linux/macOS | Linux/macOS/Windows |
|:-:|:-:|:-:|:-:|
| **Build Status** | [![Windows build status](https://img.shields.io/appveyor/ci/martincostello/lambda-test-server/master.svg)](https://ci.appveyor.com/project/martincostello/lambda-test-server) [![Code coverage](https://codecov.io/gh/martincostello/lambda-test-server/branch/master/graph/badge.svg)](https://codecov.io/gh/martincostello/lambda-test-server) | [![Linux/macOS build status](https://img.shields.io/travis/com/martincostello/lambda-test-server/master.svg)](https://travis-ci.com/martincostello/lambda-test-server) | [![Azure Pipelines build status](https://martincostello.visualstudio.com/lambda-test-server/_apis/build/status/martincostello.lambda-test-server?branchName=master)](https://dev.azure.com/martincostello/lambda-test-server/_build/latest?definitionId=74&branchName=master) |
| **Build History** | [![Windows build history](https://buildstats.info/appveyor/chart/martincostello/lambda-test-server?branch=master&includeBuildsFromPullRequest=false)](https://ci.appveyor.com/project/martincostello/lambda-test-server) | [![Linux build history](https://buildstats.info/travisci/chart/martincostello/lambda-test-server?branch=master&includeBuildsFromPullRequest=false)](https://travis-ci.com/martincostello/lambda-test-server) | [![Build history](https://buildstats.info/azurepipelines/chart/martincostello/lambda-test-server/74?branch=master&includeBuildsFromPullRequest=false)](https://dev.azure.com/martincostello/lambda-test-server/_build?definitionId=74) |

## Introduction

A NuGet package that builds on top of the `TestServer` class in the [Microsoft.AspNetCore.TestHost](https://www.nuget.org/packages/Microsoft.AspNetCore.TestHost) NuGet package to provide infrastructure to use the to end-to-end/integration test .NET Core 3.0 AWS Lambda Functions using a custom runtime using the `LambdaBootstrap` class from the [Amazon.Lambda.RuntimeSupport](https://www.nuget.org/packages/Amazon.Lambda.RuntimeSupport/) NuGet package.

[_.NET Core 3.0 on Lambda with AWS Lambda’s Custom Runtime_](https://aws.amazon.com/blogs/developer/net-core-3-0-on-lambda-with-aws-lambdas-custom-runtime/ ".NET Core 3.0 on Lambda with AWS Lambda’s Custom Runtime on the AWS Developer Blog")

### Installation

To install the library from [NuGet](https://www.nuget.org/packages/MartinCostello.Testing.AwsLambdaTestServer/ "MartinCostello.Testing.AwsLambdaTestServer on NuGet.org") using the .NET SDK run:

```
dotnet add package MartinCostello.Testing.AwsLambdaTestServer
```

### Usage

```csharp
// TODO
```

You can find more examples in the [unit tests](https://github.com/martincostello/lambda-test-server/blob/master/tests/AwsLambdaTestServer.Tests/Examples.cs "Unit test examples").

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
