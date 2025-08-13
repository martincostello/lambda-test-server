# AWS Lambda Test Server for .NET

[![NuGet][package-badge-version]][package-download]
[![NuGet Downloads][package-badge-downloads]][package-download]

[![Build status][build-badge]][build-status]
[![codecov][coverage-badge]][coverage-report]

## Introduction

A NuGet package that builds on top of the `TestServer` class in the [Microsoft.AspNetCore.TestHost][testhost] NuGet package to provide infrastructure to use with end-to-end/integration tests of .NET AWS Lambda Functions using a custom runtime with the `LambdaBootstrap` class from the [Amazon.Lambda.RuntimeSupport][lambda-runtime-support] NuGet package.

[_.NET Core 3.0 on Lambda with AWS Lambda's Custom Runtime_][custom-lambda-runtime]

## Feedback

Any feedback or issues can be added to the issues for this project in [GitHub][issues].

## License

This project is licensed under the [Apache 2.0][license] license.

[build-badge]: https://github.com/martincostello/lambda-test-server/actions/workflows/build.yml/badge.svg?branch=main&event=push
[build-status]: https://github.com/martincostello/lambda-test-server/actions/workflows/build.yml?query=branch%3Amain+event%3Apush "Continuous Integration for this project"
[coverage-badge]: https://codecov.io/gh/martincostello/lambda-test-server/branch/main/graph/badge.svg
[coverage-report]: https://codecov.io/gh/martincostello/lambda-test-server "Code coverage report for this project"
[custom-lambda-runtime]: https://aws.amazon.com/blogs/developer/net-core-3-0-on-lambda-with-aws-lambdas-custom-runtime/ ".NET Core 3.0 on Lambda with AWS Lambda's Custom Runtime on the AWS Developer Blog"
[issues]: https://github.com/martincostello/lambda-test-server/issues "Issues for this project on GitHub.com"
[lambda-runtime-support]: https://www.nuget.org/packages/Amazon.Lambda.RuntimeSupport/ "Download Amazon.Lambda.RuntimeSupport from NuGet"
[license]: https://www.apache.org/licenses/LICENSE-2.0.txt "The Apache 2.0 license"
[package-badge-downloads]: https://img.shields.io/nuget/dt/MartinCostello.Testing.AwsLambdaTestServer?logo=nuget&label=Downloads&color=blue
[package-badge-version]: https://img.shields.io/nuget/v/MartinCostello.Testing.AwsLambdaTestServer?logo=nuget&label=Latest&color=blue
[package-download]: https://www.nuget.org/packages/MartinCostello.Testing.AwsLambdaTestServer "Download MartinCostello.Testing.AwsLambdaTestServer from NuGet"
[testhost]: https://www.nuget.org/packages/Microsoft.AspNetCore.TestHost "Download Microsoft.AspNetCore.TestHost from NuGet"
