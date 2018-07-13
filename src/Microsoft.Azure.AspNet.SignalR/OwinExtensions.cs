// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Configuration;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.AspNet.SignalR;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Configuration;

namespace Owin
{
    public static partial class OwinExtensions
    {
        public static IAppBuilder MapAzureSignalR(this IAppBuilder builder)
        {
            return builder.MapAzureSignalR(new HubConfiguration());
        }

        public static IAppBuilder MapAzureSignalR(this IAppBuilder builder, HubConfiguration configuration)
        {
            return builder.MapAzureSignalR("/signalr", configuration);
        }

        public static IAppBuilder MapAzureSignalR(this IAppBuilder builder, string path, HubConfiguration configuration)
        {
            // TODO: Add auth attributes
            return builder
                .Map(path, subApp => subApp.RunAzureSignalR(configuration))
                .Map(path + "/negotiate", subApp => RedirectToService(subApp))
                ;
        }

        public static void RunAzureSignalR(this IAppBuilder builder)
        {
            builder.RunAzureSignalR(new HubConfiguration());
        }

        /// <summary>
        /// TODO: how to add /negotiate in this API?
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configuration"></param>
        public static void RunAzureSignalR(this IAppBuilder builder, HubConfiguration configuration)
        {
            RunAzureSignalRCore(builder, configuration);
        }

        private static void RedirectToService(IAppBuilder builder)
        {
            Debug.Fail("This can never be reached");
        }

        private static void RunAzureSignalRCore(IAppBuilder builder, HubConfiguration configuration)
        {
            var hubDispatcher = new ServiceHubDispatcher(configuration);
            configuration.Resolver.Register(typeof(HubDispatcher), () => hubDispatcher);
            builder.RunSignalR(typeof(HubDispatcher), configuration);

            // share the same object all through
            var serviceOptions = new ServiceOptions
            {
                ConnectionString = WebConfigurationManager.ConnectionStrings[ServiceOptions.ConnectionStringDefaultKey].ConnectionString,
            };

            var serviceProtocol = new ServiceProtocol();
            var scm = new ServiceConnectionManager();
            var endpoint = new ServiceEndpoint(serviceOptions);
            var provider = new EmptyProtectedData();

            configuration.Resolver.Register(typeof(ServiceEndpoint), () => endpoint);
            configuration.Resolver.Register(typeof(IServiceConnectionManager), () => scm);
            configuration.Resolver.Register(typeof(IProtectedData), () => provider);
            configuration.Resolver.Register(typeof(IMessageBus), () => new ServiceMessageBus(configuration.Resolver));
            configuration.Resolver.Register(typeof(ITransportManager), () => new AzureTransportManager());
            configuration.Resolver.Register(typeof(IServiceProtocol), () => serviceProtocol);

            ILoggerFactory logger;
            var traceManager = configuration.Resolver.Resolve<ITraceManager>();
            if (traceManager != null)
            {
                logger = new TraceManagerLoggerFactory(traceManager);
            }
            else
            {
                logger = new NullLoggerFactory();
            }

            var hubManager = configuration.Resolver.Resolve<IHubManager>();
            var hubs = hubManager.GetHubs().Select(s => s.Name).ToList();

            // Start the server->service connection asynchronously 
            _ = new ConnectionFactory(hubs, serviceProtocol, configuration, serviceOptions, logger).StartAsync();
        }
    }
}
