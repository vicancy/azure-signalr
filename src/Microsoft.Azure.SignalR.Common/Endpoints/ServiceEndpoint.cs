// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR
{
    public class ServiceEndpoint
    {
        internal static readonly ServiceEndpoint Empty = new ServiceEndpoint();

        public string ConnectionString { get; }

        public string Key { get; }

        public string Name { get; }

        public EndpointType EndpointType { get; }

        internal EndpointStatus Status { get; set; }

        internal string Endpoint { get; }

        internal string Version { get; }

        internal string AccessKey { get; }

        internal int? Port { get; }

        // For test purpose
        internal ServiceEndpoint() { }

        public ServiceEndpoint(string key, string connectionString)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            ConnectionString = connectionString;
            Key = key;
            (Endpoint, AccessKey, Version, Port) = ConnectionStringParser.Parse(connectionString);
            (Name, EndpointType) = ParseKey(Key);
        }

        public override int GetHashCode()
        {
            // cares about key only
            return Key.GetHashCode();
        }

        internal (string, EndpointType) ParseKey(string key)
        {
            if (key == Constants.ConnectionStringDefaultKey)
            {
                return (string.Empty, EndpointType.Primary);
            }

            if (key.StartsWith(Constants.ConnectionStringKeyPrefix))
            {
                // Azure:SignalR:ConnectionString:<name>:<type>
                var status = key.Substring(Constants.ConnectionStringKeyPrefix.Length);
                var parts = status.Split(':');
                if (parts.Length == 1)
                {
                    return (parts[0], EndpointType.Primary);
                }
                else
                {
                    if (Enum.TryParse<EndpointType>(parts[1], out var endpointStatus))
                    {
                        return (parts[0], endpointStatus);
                    }
                    else
                    {
                        return (status, EndpointType.Primary);
                    }
                }
            }

            throw new ArgumentException($"Invalid format: {key}", nameof(key));
        }
    }

}
