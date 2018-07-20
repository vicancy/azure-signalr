// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using Microsoft.Azure.SignalR;
using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceEndpoint : IServiceEndpoint
    {
        private const string EndpointProperty = "endpoint";
        private const string AccessKeyProperty = "accesskey";
        private const int ClientPort = 5001;
        private const int ServerPort = 5002;

        private static readonly char[] PropertySeparator = { ';' };
        private static readonly char[] KeyValueSeparator = { '=' };

        private static readonly string ConnectionStringNotFound =
            "No connection string was specified. " +
            $"Please specify a configuration entry for {ServiceOptions.ConnectionStringDefaultKey}, " +
            "or explicitly pass one using IServiceCollection.AddAzureSignalR(connectionString) in Startup.ConfigureServices.";

        private static readonly string MissingRequiredProperty =
            $"Connection string missing required properties {EndpointProperty} and {AccessKeyProperty}.";

        private TimeSpan AccessTokenLifetime { get; }

        public string Endpoint { get; }
        public string AccessKey { get; }

        public ServiceEndpoint(IConfigure<ServiceOptions> options)
        {
            var connectionString = options.Value.ConnectionString;
            if (connectionString == null)
            {
                throw new ArgumentException(ConnectionStringNotFound);
            }

            AccessTokenLifetime = options.Value.AccessTokenLifetime;

            (Endpoint, AccessKey) = ParseConnectionString(connectionString);
        }

        public string GenerateClientAccessToken(IEnumerable<Claim> claims = null,
            TimeSpan? lifetime = null)
        {
            return InternalGenerateAccessToken(GetClientEndpoint(), claims, lifetime ?? AccessTokenLifetime);
        }

        public string GenerateServerAccessToken(string hubName, string userId, TimeSpan? lifetime = null)
        {
            IEnumerable<Claim> claims = null;
            if (userId != null)
            {
                claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                };
            }

            return InternalGenerateAccessToken(GetServerEndpoint(hubName), claims, lifetime ?? AccessTokenLifetime);
        }

        private string InternalGenerateAccessToken(string audience, IEnumerable<Claim> claims, TimeSpan lifetime)
        {
            var expire = DateTime.UtcNow.Add(lifetime);

            return AuthenticationHelper.GenerateJwtBearer(
                audience: audience,
                claims: claims,
                expires: expire,
                signingKey: AccessKey
            );
        }

        private static (string, string) ParseConnectionString(string connectionString)
        {
            var properties = connectionString.Split(PropertySeparator, StringSplitOptions.RemoveEmptyEntries);
            if (properties.Length > 1)
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in properties)
                {
                    var kvp = property.Split(KeyValueSeparator, 2);
                    if (kvp.Length != 2) continue;

                    var key = kvp[0].Trim();
                    if (dict.ContainsKey(key))
                    {
                        throw new ArgumentException($"Duplicate properties found in connection string: {key}.");
                    }

                    dict.Add(key, kvp[1].Trim());
                }

                if (dict.ContainsKey(EndpointProperty) && dict.ContainsKey(AccessKeyProperty))
                {
                    return (dict[EndpointProperty].TrimEnd('/'), dict[AccessKeyProperty]);
                }
            }

            throw new ArgumentException(MissingRequiredProperty);
        }

        public string GetServerEndpoint(string hubName)
        {
            return $"{Endpoint}:{ServerPort}/v2/server/?hub={hubName.ToLower()}";
        }

        public string GetClientEndpoint()
        {
            return $"{Endpoint}:{ClientPort}/v2/client";
        }

    }
}
