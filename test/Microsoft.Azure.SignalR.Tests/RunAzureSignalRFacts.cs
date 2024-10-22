// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests;

public class RunAzureSignalRFacts : VerifiableLoggedTest
{
    private const string CustomValue = "Endpoint=https://customconnectionstring;AccessKey=1";
    private const string DefaultValue = "Endpoint=https://defaultconnectionstring;AccessKey=1";
    private const string SecondaryValue = "Endpoint=https://secondaryconnectionstring;AccessKey=1";
    private const string ConfigFile = "testappsettings.json";

    public RunAzureSignalRFacts(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void RunAzureSignalR()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                {"Azure:SignalR:ConnectionString", DefaultValue},
                {"Azure:SignalR:ApplicationName", "Application1"}
                })
                .Build();
            var serviceProvider = services.AddSignalR()
                .AddAzureSignalR()
                .Services
                .AddSingleton<IConfiguration>(config)
                .AddSingleton(loggerFactory)
                .BuildServiceProvider();

            var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

            Assert.Equal(DefaultValue, options.ConnectionString);
            Assert.Equal(5, options.ConnectionCount);
            Assert.Equal("Application1", options.ApplicationName);
            Assert.Equal(5, options.InitialHubServerConnectionCount);
            Assert.Equal(TimeSpan.FromHours(1), options.AccessTokenLifetime);
            Assert.Null(options.ClaimsProvider);
        }
    }
}