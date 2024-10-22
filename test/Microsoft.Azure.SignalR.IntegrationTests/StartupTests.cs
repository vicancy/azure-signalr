// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Azure.SignalR.IntegrationTests.Infrastructure;
using Microsoft.Azure.SignalR.IntegrationTests.Infrastructure.MessageOrderTests;
using Microsoft.Azure.SignalR.IntegrationTests.MockService;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.IntegrationTests
{
    public class StartupTests : VerifiableLoggedTest
    {
        private readonly ITestOutputHelper _output;

        public StartupTests(ITestOutputHelper output) : base(output)
        {
            _output = output;
        }

        private class StartupTestParameters : IIntegrationTestStartupParameters
        {
            public static int ConnectionCount = 2;
            public static GracefulShutdownMode ShutdownMode = GracefulShutdownMode.Off;
            private static ServiceEndpoint[] ServiceEndpoints = [
                new ServiceEndpoint("Endpoint=http://127.0.0.1;AccessKey=AAAAAAAAAAAAAAAAAAAAAAAAAA0A2A4A6A8A;Version=1.0;Port=8080", type: EndpointType.Primary, name: "primary"),
            ];

            int IIntegrationTestStartupParameters.ConnectionCount => 1;
            ServiceEndpoint[] IIntegrationTestStartupParameters.ServiceEndpoints => ServiceEndpoints;
            GracefulShutdownMode IIntegrationTestStartupParameters.ShutdownMode => GracefulShutdownMode.Off;
        }

        private class EndlessConnectHub : Hub
        {

            public override async Task OnConnectedAsync()
            {
                await Clients.Group("note").SendAsync("hello");
                //while (true)
                //{
                //    await Task.Delay(1000);
                //}
            }
        }

        [Fact]
        // verifies that outgoing messages from a hub call:
        // - use the same primary service connection the hub call was made from
        // - pick and stick to the same secondary connection(s)
        public async Task StartupTest()
        {
            var builder = WebHost.CreateDefaultBuilder()
                .ConfigureServices((IServiceCollection services) => { })
                .ConfigureLogging(logging => logging.AddXunit(_output))
                .UseStartup<IntegrationTestStartup<StartupTestParameters, EndlessConnectHub>>();

            using (var server = new TestServer(builder))
            {
                var serviceHubDispatcher = (server.Host.Services.GetRequiredService<ServiceHubDispatcher<EndlessConnectHub>>()
                    as MockServiceHubDispatcher<EndlessConnectHub>).MockService;
                await serviceHubDispatcher.AllConnectionsEstablished().OrTimeout();
                var serverConnections = serviceHubDispatcher.ServiceSideConnections;

                Assert.Single(serverConnections);

                // specify invocation binder before making calls
                serviceHubDispatcher.CurrentInvocationBinder = new TestHubBroadcastNCallsInvocationBinder();

                await using var serverConnection = serverConnections[0];
                var client = await serverConnection.ConnectClientAsync().OrTimeout();
            }

            await Task.Delay(2000);
        }
    }
}
