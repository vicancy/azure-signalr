// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceOptionsSetup : IConfigureOptions<ServiceOptions>
    {
        private static readonly string ConnectionStringSecondaryKey = Constants.ConnectionStringsPrefix + Constants.ConnectionStringDefaultKey;

        private static readonly string ConnectionStringPrefixSecondaryKey = Constants.ConnectionStringsPrefix + Constants.ConnectionStringKeyPrefix;

        private static readonly string ConnectionStringNotFound =
            "No connection string was specified. " +
            $"Please specify a configuration entry for {ServiceOptions.ConnectionStringDefaultKey}, " +
            "or explicitly pass one using IServiceCollection.AddAzureSignalR(connectionString) in Startup.ConfigureServices.";

        private ServiceEndpoint[] _endpoints;

        public ServiceOptionsSetup(IConfiguration configuration)
        {
            var connectionString = configuration.GetSection(Constants.ConnectionStringDefaultKey).Value;

            // Load connection string from "ConnectionStrings" section when default key doesn't exist or holds an empty value.
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = configuration.GetSection(ConnectionStringSecondaryKey).Value;
            }

            var endpoints = new List<ServiceEndpoint>();
            foreach(var section in configuration.AsEnumerable())
            {
                if (string.Equals(section.Key, Constants.ConnectionStringDefaultKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(section.Key, ConnectionStringSecondaryKey, StringComparison.OrdinalIgnoreCase)
                    || section.Key.StartsWith(Constants.ConnectionStringKeyPrefix, StringComparison.OrdinalIgnoreCase)
                    || section.Key.StartsWith(ConnectionStringPrefixSecondaryKey, StringComparison.OrdinalIgnoreCase)
                    )
                {
                    endpoints.Add(new ServiceEndpoint(section.Key, section.Value));
                }
            }

            if (endpoints.GroupBy(s => s.ConnectionString).Any(s => s.Count() > 1))
            {
                throw new ArgumentException("Options should not have the duplicate connection string");
            }

            _endpoints = endpoints.ToArray();
        }

        public void Configure(ServiceOptions options)
        {
            if (options.Endpoints == null)
            {
                options.Endpoints = _endpoints.Select(s => new ServiceEndpoint(s, options.AccessTokenLifetime)).ToArray();
            }

            if (options.ConnectionString == null)
            {
                options.ConnectionString = 
                    _endpoints.FirstOrDefault(s => s.Key == Constants.ConnectionStringDefaultKey)?.ConnectionString 
                    ?? _endpoints.FirstOrDefault()?.ConnectionString;

            }

            if (string.IsNullOrEmpty(options.ConnectionString) || options.Endpoints.Length == 0)
            {
                throw new ArgumentException(ConnectionStringNotFound);
            }
        }
    }
}
