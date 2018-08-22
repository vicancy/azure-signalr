// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hosting;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.AspNet;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Owin.Extensions;
using Microsoft.Owin.Infrastructure;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

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
            var hubManager = configuration.Resolver.Resolve<IHubManager>();
            var hubs = hubManager.GetHubs().Select(s => s.Name).ToList();

            // If we don't get a valid instance name then generate a random one
            var instanceName = (builder.Properties.GetAppInstanceName() ?? Guid.NewGuid().ToString("N")).ToLower();

            builder.UseSignalRMiddleware<ServiceHubMiddleware>(instanceName, configuration);

            // share the same object all through
            var serviceOptions = new Configure<ServiceOptions>(new ServiceOptions
            {
                ConnectionString = ConfigurationManager.ConnectionStrings[ServiceOptions.ConnectionStringDefaultKey].ConnectionString,
            });

            var serviceProtocol = new ServiceProtocol();
            var endpoint = new ServiceEndpoint(serviceOptions);
            var provider = new EmptyProtectedData();

            var scm = new ServiceConnectionManager();

            configuration.Resolver.Register(typeof(IConfigure<ServiceOptions>), () => serviceOptions);
            configuration.Resolver.Register(typeof(IServiceEndpoint), () => endpoint);
            configuration.Resolver.Register(typeof(IServiceConnectionManager), () => scm);
            configuration.Resolver.Register(typeof(IProtectedData), () => provider);
            configuration.Resolver.Register(typeof(IMessageBus), () => new ServiceMessageBus(configuration.Resolver));
            configuration.Resolver.Register(typeof(ITransportManager), () => new AzureTransportManager());
            configuration.Resolver.Register(typeof(IServiceProtocol), () => serviceProtocol);

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

            // Start the server->service connection asynchronously 
            _ = new ConnectionFactory(instanceName, hubs, configuration, logger).StartAsync();
        }

        private static IAppBuilder UseSignalRMiddleware<T>(this IAppBuilder builder, params object[] args)
        {
            EnsureValidCulture();
            ConnectionConfiguration configuration = null;

            // Ensure we have the conversions for MS.Owin so that
            // the app builder respects the OwinMiddleware base class
            SignatureConversions.AddConversions(builder);

            if (args.Length > 0)
            {
                configuration = args[args.Length - 1] as ConnectionConfiguration;

                if (configuration == null)
                {
                    throw new ArgumentException("A configuration object must be specified.");
                }

                var resolver = configuration.Resolver;

                if (resolver == null)
                {
                    throw new ArgumentException("No dependency resolver is found");
                }

                var env = builder.Properties;
                CancellationToken token = env.GetShutdownToken();

                // If we don't get a valid instance name then generate a random one
                string instanceName = env.GetAppInstanceName() ?? Guid.NewGuid().ToString();


                // If the host provides trace output then add a default trace listener
                TextWriter traceOutput = env.GetTraceOutput();
                if (traceOutput != null)
                {
                    var hostTraceListener = new TextWriterTraceListener(traceOutput);
                    var traceManager = new TraceManager(hostTraceListener);
                    resolver.Register(typeof(ITraceManager), () => traceManager);
                }

                // Try to get the list of reference assemblies from the host
                IEnumerable<Assembly> referenceAssemblies = env.GetReferenceAssemblies();
                if (referenceAssemblies != null)
                {
                    // Use this list as the assembly locator
                    var assemblyLocator = new EnumerableOfAssemblyLocator(referenceAssemblies);
                    resolver.Register(typeof(IAssemblyLocator), () => assemblyLocator);
                }

                resolver.InitializeHost(instanceName, token);
            }

            builder.Use(typeof(T), args);

            // BUG 2306: We need to make that SignalR runs before any handlers are
            // mapped in the IIS pipeline so that we avoid side effects like
            // session being enabled. The session behavior can be
            // manually overridden if user calls SetSessionStateBehavior but that shouldn't
            // be a problem most of the time.
            builder.UseStageMarker(PipelineStage.PostAuthorize);

            return builder;
        }

        private static void EnsureValidCulture()
        {
            // The CultureInfo may leak across app domains which may cause hangs. The most prominent
            // case in SignalR are MapSignalR hangs when creating Performance Counters (#3414).
            // See https://github.com/SignalR/SignalR/issues/3414#issuecomment-152733194 for more details.
            var culture = CultureInfo.CurrentCulture;
            while (!culture.Equals(CultureInfo.InvariantCulture))
            {
                culture = culture.Parent;
            }

            if (ReferenceEquals(culture, CultureInfo.InvariantCulture))
            {
                return;
            }

            var thread = Thread.CurrentThread;
            thread.CurrentCulture = CultureInfo.GetCultureInfo(thread.CurrentCulture.Name);
            thread.CurrentUICulture = CultureInfo.GetCultureInfo(thread.CurrentUICulture.Name);
        }
    }


    internal static class OwinEnvironmentExtensions
    {
        internal static CancellationToken GetShutdownToken(this IDictionary<string, object> env)
        {
            object value;
            return env.TryGetValue(OwinConstants.HostOnAppDisposing, out value)
                && value is CancellationToken
                ? (CancellationToken)value
                : default(CancellationToken);
        }

        internal static string GetAppInstanceName(this IDictionary<string, object> environment)
        {
            object value;
            if (environment.TryGetValue(OwinConstants.HostAppNameKey, out value))
            {
                var stringVal = value as string;

                if (!String.IsNullOrEmpty(stringVal))
                {
                    return stringVal;
                }
            }

            return null;
        }

        internal static TextWriter GetTraceOutput(this IDictionary<string, object> environment)
        {
            object value;
            if (environment.TryGetValue(OwinConstants.HostTraceOutputKey, out value))
            {
                return value as TextWriter;
            }

            return null;
        }

        internal static bool SupportsWebSockets(this IDictionary<string, object> environment)
        {
            object value;
            if (environment.TryGetValue(OwinConstants.ServerCapabilities, out value))
            {
                var capabilities = value as IDictionary<string, object>;
                if (capabilities != null)
                {
                    return capabilities.ContainsKey(OwinConstants.WebSocketVersion);
                }
            }
            return false;
        }

        internal static bool IsDebugEnabled(this IDictionary<string, object> environment)
        {
            object value;
            if (environment.TryGetValue(OwinConstants.HostAppModeKey, out value))
            {
                var stringVal = value as string;
                return !String.IsNullOrWhiteSpace(stringVal) &&
                       OwinConstants.AppModeDevelopment.Equals(stringVal, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        internal static IEnumerable<Assembly> GetReferenceAssemblies(this IDictionary<string, object> environment)
        {
            object assembliesValue;
            if (environment.TryGetValue(OwinConstants.HostReferencedAssembliesKey, out assembliesValue))
            {
                return (IEnumerable<Assembly>)assembliesValue;
            }

            return null;
        }

        internal static void DisableResponseBuffering(this IDictionary<string, object> environment)
        {
            environment.Get<Action>(OwinConstants.DisableResponseBuffering)?.Invoke();
        }

        internal static void DisableRequestCompression(this IDictionary<string, object> environment)
        {
            environment.Get<Action>(OwinConstants.DisableRequestCompression)?.Invoke();
        }

        internal static class OwinConstants
        {
            public const string Version = "owin.Version";

            public const string RequestBody = "owin.RequestBody";
            public const string RequestHeaders = "owin.RequestHeaders";
            public const string RequestScheme = "owin.RequestScheme";
            public const string RequestMethod = "owin.RequestMethod";
            public const string RequestPathBase = "owin.RequestPathBase";
            public const string RequestPath = "owin.RequestPath";
            public const string RequestQueryString = "owin.RequestQueryString";
            public const string RequestProtocol = "owin.RequestProtocol";

            public const string CallCancelled = "owin.CallCancelled";

            public const string ResponseStatusCode = "owin.ResponseStatusCode";
            public const string ResponseReasonPhrase = "owin.ResponseReasonPhrase";
            public const string ResponseHeaders = "owin.ResponseHeaders";
            public const string ResponseBody = "owin.ResponseBody";

            public const string TraceOutput = "host.TraceOutput";

            public const string User = "server.User";
            public const string RemoteIpAddress = "server.RemoteIpAddress";
            public const string RemotePort = "server.RemotePort";
            public const string LocalIpAddress = "server.LocalIpAddress";
            public const string LocalPort = "server.LocalPort";

            public const string DisableRequestCompression = "systemweb.DisableResponseCompression";
            public const string DisableRequestBuffering = "server.DisableRequestBuffering";
            public const string DisableResponseBuffering = "server.DisableResponseBuffering";

            public const string ServerCapabilities = "server.Capabilities";
            public const string WebSocketVersion = "websocket.Version";
            public const string WebSocketAccept = "websocket.Accept";

            public const string HostOnAppDisposing = "host.OnAppDisposing";
            public const string HostAppNameKey = "host.AppName";
            public const string HostAppModeKey = "host.AppMode";
            public const string HostTraceOutputKey = "host.TraceOutput";
            public const string HostReferencedAssembliesKey = "host.ReferencedAssemblies";
            public const string AppModeDevelopment = "development";
        }
    }

    internal static class RequestExtensions
    {
        internal static T Get<T>(this IDictionary<string, object> values, string key)
        {
            object value;
            return values.TryGetValue(key, out value) ? (T)value : default(T);
        }
    }
}
