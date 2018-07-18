// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Configuration;
using Microsoft.AspNet.SignalR.Hosting;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Json;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Microsoft.Azure.AspNet.SignalR
{
    internal class ServiceHubDispatcher : HubDispatcher
    {
        private const string WebSocketsTransportName = "webSockets";
        private static readonly ProtocolResolver _protocolResolver = new ProtocolResolver();

        private IConfigurationManager _configurationManager;
        private IServiceEndpoint _endpoint;

        public ServiceHubDispatcher(HubConfiguration configuration) : base(configuration)
        {
        }

        public override void Initialize(IDependencyResolver resolver)
        {
            _endpoint = resolver.Resolve<IServiceEndpoint>();
            _configurationManager = resolver.Resolve<IConfigurationManager>();
            base.Initialize(resolver);
        }

        public override Task ProcessRequest(HostContext context)
        {
            if (IsNegotiationRequest(context.Request))
            {
                return ProcessNegotiationRequest(context);
            }
            return base.ProcessRequest(context);
        }

        private Task ProcessNegotiationRequest(HostContext context)
        {
            // Total amount of time without a keep alive before the client should attempt to reconnect in seconds.
            var keepAliveTimeout = _configurationManager.KeepAliveTimeout();
            var user = new Owin.OwinContext(context.Environment).Authentication.User;
            var claims = user?.Claims;
            var authenticationType = user?.Identity?.AuthenticationType;
            var userId = UserIdProvider.GetUserId(context.Request);
            var advancedClaims = claims.ToList();

            advancedClaims.Add(new Claim("azure.signalr.authenticationtype", authenticationType));
            advancedClaims.Add(new Claim("azure.signalr.userid", userId));

            var payload = new
            {
                // Redirect to Service
                Url = _endpoint.GetClientEndpoint(),
                AccessToken = _endpoint.GenerateClientAccessToken(advancedClaims),

                // Configs
                KeepAliveTimeout = keepAliveTimeout != null ? keepAliveTimeout.Value.TotalSeconds : (double?)null,
                DisconnectTimeout = _configurationManager.DisconnectTimeout.TotalSeconds,
                ConnectionTimeout = _configurationManager.ConnectionTimeout.TotalSeconds,
                TransportConnectTimeout = _configurationManager.TransportConnectTimeout.TotalSeconds,
                LongPollDelay = _configurationManager.LongPollDelay.TotalSeconds,
                ProtocolVersion = _protocolResolver.Resolve(context.Request).ToString(),
            };

            return SendJsonResponse(context, JsonSerializer.Stringify(payload));
        }

        private static bool IsNegotiationRequest(IRequest request)
        {
            return request.LocalPath.EndsWith("/negotiate", StringComparison.OrdinalIgnoreCase);
        }

        private static Task SendJsonResponse(HostContext context, string jsonPayload)
        {
            var callback = context.Request.QueryString["callback"];
            if (String.IsNullOrEmpty(callback))
            {
                // Send normal JSON response
                context.Response.ContentType = JsonUtility.JsonMimeType;
                return context.Response.End(jsonPayload);
            }

            // Send JSONP response since a callback is specified by the query string
            var callbackInvocation = JsonUtility.CreateJsonpCallback(callback, jsonPayload);
            context.Response.ContentType = JsonUtility.JavaScriptMimeType;
            return context.Response.End(callbackInvocation);
        }
    }
}
