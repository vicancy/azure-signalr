// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.AspNet;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Owin
{
    public static partial class OwinExtensions
    {
        /// <summary>
        /// Maps Azure SignalR hubs to the app builder pipeline at "/signalr".
        /// </summary>
        /// <param name="builder">The app builder</param>
        /// <param name="appName">The name of your app, it is case-incensitive</param>
        /// <returns>The app builder</returns>
        public static IAppBuilder MapAzureSignalR(this IAppBuilder builder, string appName)
        {
            return builder.MapAzureSignalR(appName, new HubConfiguration());
        }

        /// <summary>
        /// Maps Azure SignalR hubs to the app builder pipeline at "/signalr".
        /// </summary>
        /// <param name="builder">The app builder</param>
        /// <param name="appName">The name of your app, it is case-incensitive</param>
        /// <param name="configuration">The hub configuration</param>
        /// <returns>The app builder</returns>
        public static IAppBuilder MapAzureSignalR(this IAppBuilder builder, string appName, HubConfiguration configuration)
        {
            return builder.MapAzureSignalR("/signalr", appName, configuration);
        }

        /// <summary>
        /// Maps Azure SignalR hubs to the app builder pipeline at the specified path.
        /// </summary>
        /// <param name="builder">The app builder</param>
        /// <param name="path">The path to map signalr hubs</param>
        /// <param name="appName">The name of your app, it is case-incensitive</param>
        /// <param name="configuration">The hub configuration</param>
        /// <returns>The app builder</returns>
        public static IAppBuilder MapAzureSignalR(this IAppBuilder builder, string path, string appName, HubConfiguration configuration)
        {
            return builder.Map(path, subApp => subApp.RunAzureSignalR(appName, configuration));
        }

        /// <summary>
        /// Adds Azure SignalR hubs to the app builder pipeline at "/signalr".
        /// </summary>
        /// <param name="builder">The app builder</param>
        /// <param name="appName">The name of your app, it is case-incensitive</param>
        public static void RunAzureSignalR(this IAppBuilder builder, string appName)
        {
            builder.RunAzureSignalR(appName, new HubConfiguration());
        }

        /// <summary>
        /// Adds Azure SignalR hubs to the app builder pipeline at "/signalr".
        /// </summary>
        /// <param name="builder">The app builder</param>
        /// <param name="appName">The name of your app, it is case-incensitive</param>
        /// <param name="connectionString">The connection string of an Azure SignalR Service instance.</param>
        public static void RunAzureSignalR(this IAppBuilder builder, string appName, string connectionString)
        {
            RunAzureSignalR(builder, appName, connectionString, new HubConfiguration());
        }

        /// <summary>
        /// Adds Azure SignalR hubs to the app builder pipeline at "/signalr" using the connection string specified in web.config 
        /// </summary>
        /// <param name="builder">The app builder</param>
        /// <param name="appName">The name of your app, it is case-incensitive</param>
        /// <param name="configuration">The hub configuration</param>
        public static void RunAzureSignalR(this IAppBuilder builder, string appName, HubConfiguration configuration)
        {
            RunAzureSignalR(builder, appName, ConfigurationManager.ConnectionStrings[ServiceOptions.ConnectionStringDefaultKey]?.ConnectionString, configuration);
        }

        /// <summary>
        /// Adds Azure SignalR hubs to the app builder pipeline at "/signalr".
        /// </summary>
        /// <param name="builder">The app builder</param>
        /// <param name="appName">The name of your app, it is case-incensitive</param>
        /// <param name="connectionString">The connection string of an Azure SignalR Service instance.</param>
        /// <param name="configuration">The hub configuration</param>
        public static void RunAzureSignalR(this IAppBuilder builder, string appName, string connectionString, HubConfiguration configuration)
        {
            RunAzureSignalR(builder, appName, configuration, s => s.ConnectionString = connectionString);
        }

        /// <summary>
        /// Adds Azure SignalR hubs to the app builder pipeline at "/signalr".
        /// </summary>
        /// <param name="builder">The app builder</param>
        /// <param name="appName">The name of your app, it is case-incensitive</param>
        /// <param name="configuration">The hub configuration</param>
        /// <param name="optionsConfigure">A callback to configure the <see cref="ServiceOptions"/>.</param>
        public static void RunAzureSignalR(this IAppBuilder builder, string appName, HubConfiguration configuration, Action<ServiceOptions> optionsConfigure)
        {
            var serviceOptions = new ServiceOptions();
            optionsConfigure?.Invoke(serviceOptions);
            RunAzureSignalRCore(builder, appName, configuration, serviceOptions);
        }

        private static void RunAzureSignalRCore(IAppBuilder builder, string appName, HubConfiguration configuration, ServiceOptions options)
        {
            if (string.IsNullOrEmpty(appName))
            {
                throw new ArgumentNullException(nameof(appName), "Empty app name is not allowed.");
            }

            appName = appName.ToLower();

            var hubs = GetAvailableHubNames(configuration);

            // TODO: Update to use Middleware when SignalR SDK is ready
            // Replace default HubDispatcher with a custom one, which has its own negotiation logic
            // https://github.com/SignalR/SignalR/blob/dev/src/Microsoft.AspNet.SignalR.Core/Hosting/PersistentConnectionFactory.cs#L42
            configuration.Resolver.Register(typeof(PersistentConnection), () => new ServiceHubDispatcher(configuration, appName));
            builder.RunSignalR(typeof(PersistentConnection), configuration);

            RegisterServiceObjects(configuration, options, appName, hubs);

            ILoggerFactory logger;
            var traceManager = configuration.Resolver.Resolve<ITraceManager>();
            if (traceManager != null)
            {
                logger = new LoggerFactory(new ILoggerProvider[] { new TraceManagerLoggerProvider(traceManager) });
            }
            else
            {
                logger = new NullLoggerFactory();
            }

            if (hubs?.Count > 0)
            {
                // Start the server->service connection asynchronously
                _ = new ConnectionFactory(hubs, configuration, logger).StartAsync();
            }
            else
            {
                // TODO: log something
                logger.CreateLogger<IAppBuilder>().Log(LogLevel.Information, "No hub is found.");
            }
        }

        private static void RegisterServiceObjects(HubConfiguration configuration, ServiceOptions options, string appName, IReadOnlyList<string> hubs)
        {
            // TODO: Using IOptions looks wierd, thinking of a way removing it
            // share the same object all through
            var serviceOptions = Options.Create(options);

            var serviceProtocol = new ServiceProtocol();
            var endpoint = new ServiceEndpointProvider(serviceOptions.Value);
            var provider = new EmptyProtectedData();
            var scm = new ServiceConnectionManager(appName, hubs);
            var ccm = new ClientConnectionManager(configuration);

            // For safety, ALWAYS register abstract classes or interfaces
            // Some third-party DI frameworks such as Ninject, implicit self-binding concrete types:
            // https://github.com/ninject/ninject/wiki/dependency-injection-with-ninject#skipping-the-type-binding-bit--implicit-self-binding-of-concrete-types
            configuration.Resolver.Register(typeof(IOptions<ServiceOptions>), () => serviceOptions);
            configuration.Resolver.Register(typeof(IServiceEndpointProvider), () => endpoint);
            configuration.Resolver.Register(typeof(IServiceConnectionManager), () => scm);
            configuration.Resolver.Register(typeof(IClientConnectionManager), () => ccm);
            configuration.Resolver.Register(typeof(IProtectedData), () => provider);
            configuration.Resolver.Register(typeof(IMessageBus), () => new ServiceMessageBus(configuration.Resolver));
            configuration.Resolver.Register(typeof(ITransportManager), () => new AzureTransportManager(configuration.Resolver));
            configuration.Resolver.Register(typeof(IServiceProtocol), () => serviceProtocol);
        }

        private static IReadOnlyList<string> GetAvailableHubNames(HubConfiguration configuration)
        {
            var hubManager = configuration.Resolver.Resolve<IHubManager>();
            return hubManager?.GetHubs().Select(s => s.Name).ToList();
        }
    }
}
