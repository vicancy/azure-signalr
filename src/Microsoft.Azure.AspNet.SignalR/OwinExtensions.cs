// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.AspNet.SignalR;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            // TODO: return negotiate response through Middleware
        }

        private static void RunAzureSignalRCore(IAppBuilder builder, HubConfiguration configuration)
        {
            builder.RunSignalR(configuration);
            StartServiceConnection(configuration);
        }

        private static void StartServiceConnection(HubConfiguration configuration)
        {
            var hubManager = configuration.Resolver.Resolve<IHubManager>();
            var hubs = hubManager.GetHubs().Select(s => s.HubType.Name).ToList();
            // 1. How to get configurations?
            // 2. How to do authentication?
            // 3. How to do logging?

            // TODO: use local key vault as fallback
            var serviceOptions = new ServiceOptions { ConnectionString = "Endpoint=localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;" };

            var serviceProtocol = new ServiceProtocol();

            // share the same object all through
            var scm = new ServiceConnectionManager();

            configuration.Resolver.Register(typeof(IServiceConnectionManager), () => scm);

            configuration.Resolver.Register(typeof(IProtectedData), () => new EmptyProtectedData());

            configuration.Resolver.Register(typeof(IMessageBus), () => new ServiceMessageBus(configuration.Resolver));

            configuration.Resolver.Register(typeof(ITransportManager), () => new AzureTransportManager());

            var connectionFactory = new ConnectionFactory(hubs, serviceProtocol, configuration, serviceOptions, new NullLoggerFactory());

            _ = connectionFactory.StartAsync();
        }
    }
}
