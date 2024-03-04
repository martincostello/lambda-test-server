# AWS Lambda Test Server for .NET

[![NuGet](https://buildstats.info/nuget/MartinCostello.Testing.AwsLambdaTestServer?includePreReleases=true)](https://www.nuget.org/packages/MartinCostello.Testing.AwsLambdaTestServer "Download MartinCostello.Testing.AwsLambdaTestServer from NuGet")
[![Build status](https://github.com/martincostello/lambda-test-server/workflows/build/badge.svg?branch=main&event=push)](https://github.com/martincostello/lambda-test-server/actions?query=workflow%3Abuild+branch%3Amain+event%3Apush)
[![codecov](https://codecov.io/gh/martincostello/lambda-test-server/branch/main/graph/badge.svg)](https://codecov.io/gh/martincostello/lambda-test-server)

## Introduction

A NuGet package that builds on top of the `TestServer` class in the [Microsoft.AspNetCore.TestHost](https://www.nuget.org/packages/Microsoft.AspNetCore.TestHost) NuGet package to provide infrastructure to use with end-to-end/integration tests of .NET 6 AWS Lambda Functions using a custom runtime with the `LambdaBootstrap` class from the [Amazon.Lambda.RuntimeSupport](https://www.nuget.org/packages/Amazon.Lambda.RuntimeSupport/) NuGet package.

[_.NET Core 3.0 on Lambda with AWS Lambda's Custom Runtime_](https://aws.amazon.com/blogs/developer/net-core-3-0-on-lambda-with-aws-lambdas-custom-runtime/ ".NET Core 3.0 on Lambda with AWS Lambda's Custom Runtime on the AWS Developer Blog")

## Feedback

Any feedback or issues for this package can be added to the issues in [GitHub](https://github.com/martincostello/lambda-test-server/issues "Issues for this package on GitHub.com").

## License

This package is licensed under the [Apache 2.0](https://www.apache.org/licenses/LICENSE-2.0.txt "The Apache 2.0 license") license.
