// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.IntegrationTests.Infrastructure;
using Microsoft.Azure.SignalR.IntegrationTests.Infrastructure.MessageOrderTests;
using Microsoft.Azure.SignalR.IntegrationTests.MockService;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using AspNetTestServer = Microsoft.AspNetCore.TestHost.TestServer;

namespace Microsoft.Azure.SignalR.IntegrationTests
{
    public class HubDispatcherTests : VerifiableLoggedTest
    {
        private readonly ITestOutputHelper _output;

        public HubDispatcherTests(ITestOutputHelper output) : base(output)
        {
            _output = output;
        }

        private sealed class HubDispatcherTestParams : IIntegrationTestStartupParameters
        {
            public int ConnectionCount { get; } = 1;
            public ServiceEndpoint[] ServiceEndpoints { get; } =
            [
                new ServiceEndpoint("Endpoint=http://127.0.0.1;AccessKey=AAAAAAAAAAAAAAAAAAAAAAAAAA0A2A4A6A8A;Version=1.0;Port=8080", type: EndpointType.Primary, name: "primary"),
            ];
            public GracefulShutdownMode ShutdownMode { get; }
        }

        private class Hub1 : Hub
        {
            private readonly ILogger<Hub1> _logger;

            public Hub1(ILogger<Hub1> logger)
            {
                _logger = logger;
            }

            public override Task OnConnectedAsync()
            {
                _logger.LogInformation("OnConnectedAsync");
                return base.OnConnectedAsync();
            }

            public override Task OnDisconnectedAsync(Exception exception)
            {
                _logger.LogInformation("OnDisconnectedAsync");
                return base.OnDisconnectedAsync(exception);
            }
        }

        [Fact]
        // verifies that outgoing messages from a hub call:
        // - use the same primary service connection the hub call was made from
        // - pick and stick to the same secondary connection(s)
        public async Task OutgoingMessagesUseSameServiceConnection()
        {
            var builder = WebHost.CreateDefaultBuilder()
                .ConfigureServices((IServiceCollection services) =>{
                })
                .ConfigureLogging(logging => logging.AddXunit(_output))
                .UseStartup<IntegrationTestStartup<HubDispatcherTestParams, Hub1>>();

            using (var server = new AspNetTestServer(builder))
            {
                var mockSvc = (server.Host.Services.GetRequiredService<ServiceHubDispatcher<Hub1>>()
                    as MockServiceHubDispatcher<Hub1>).MockService;
                await mockSvc.AllConnectionsEstablished().OrTimeout();
                List<MockServiceSideConnection> allSvcConns = mockSvc.ServiceSideConnections;

                int endpointCount = allSvcConns.Distinct(new MockServiceSideConnectionEndpointComparer()).Count();

                // pick a random primary svc connection to make a client connection
                var priList = allSvcConns.Where(i => i.Endpoint.EndpointType == EndpointType.Primary).ToList();
                await using var primarySvc0 = priList[StaticRandom.Next(priList.Count)];
                var client0 = await primarySvc0.ConnectClientAsync().OrTimeout();
               // await clientFlowControlTcs.Task.OrTimeout();
            }

            // client RunHub should throw
        }
    }
}
