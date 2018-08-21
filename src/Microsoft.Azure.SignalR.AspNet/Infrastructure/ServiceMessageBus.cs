﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Json;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.Protocol;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceMessageBus : MessageBus
    {
        private readonly JsonSerializer _serializer;
        private readonly IServiceConnectionManager _serviceConnectionManager;
        private readonly IAckHandler _ackHandler;
        private readonly IServiceProtocol _serviceProtocol;
        private readonly IMemoryPool _pool;

        public ServiceMessageBus(IDependencyResolver resolver) : base(resolver)
        {
            _serviceConnectionManager = resolver.Resolve<IServiceConnectionManager>() ?? throw new ArgumentNullException(nameof(IServiceConnectionManager));
            _serializer = resolver.Resolve<JsonSerializer>() ?? throw new ArgumentNullException(nameof(JsonSerializer));
            _ackHandler = resolver.Resolve<IAckHandler>() ?? throw new ArgumentNullException(nameof(IAckHandler));
            _serviceProtocol = resolver.Resolve<IServiceProtocol>() ?? throw new ArgumentNullException(nameof(IServiceProtocol));
            _pool = resolver.Resolve<IMemoryPool>() ?? throw new ArgumentNullException(nameof(IMemoryPool));
        }

        public override Task Publish(Message message)
        {
            if (message.IsCommand)
            {
                return PublishCommandAsync(message);
            }

            var segment = GetPayload(message);

            // broadcast case
            if (TryGetName(message.Key, PrefixHelper.HubPrefix, out var hubName))
            {
                return _serviceConnectionManager.WithHub(hubName).WriteAsync(new BroadcastDataMessage(excludedList: GetExcludedIds(message.Filter), payloads: GetPayloads(segment)));
            }

            // echo case
            if (TryGetName(message.Key, PrefixHelper.HubConnectionIdPrefix, out var connectionWithHubPrefix))
            {
                // naming: hc-{HubName}.{ConnectionId}
                var connections = _serviceConnectionManager.GetPossibleConnections(connectionWithHubPrefix);
                return Task.WhenAll(connections.Select(pair => pair.Item1.WriteAsync(new ConnectionDataMessage(pair.Item2, segment))));
            }

            // group broadcast case
            if (TryGetName(message.Key, PrefixHelper.HubGroupPrefix, out var groupWithHubPrefix))
            {
                var connections = _serviceConnectionManager.GetPossibleConnections(groupWithHubPrefix);

                // naming: hg-{HubName}.{GroupName}, it as a whole is the group name per the JoinGroup implementation
                return Task.WhenAll(
                    connections.Select(pair => pair.Item1.WriteAsync(
                    new GroupBroadcastDataMessage(message.Key, excludedList: GetExcludedIds(message.Filter), payloads: GetPayloads(segment)))));
            }

            // user case
            if (TryGetName(message.Key, PrefixHelper.HubUserPrefix, out var userWithHubPrefix))
            {
                // naming: hu-{HubName}.{UserName}, HubName can contain '.' and UserName can contain '.'
                // Go through all the possibilities
                var connections = _serviceConnectionManager.GetPossibleConnections(userWithHubPrefix);

                // For old protocol, it is always single user per message https://github.com/SignalR/SignalR/blob/dev/src/Microsoft.AspNet.SignalR.Core/Infrastructure/Connection.cs#L162
                return Task.WhenAll(connections.Select(pair => pair.Item1.WriteAsync(new UserDataMessage(pair.Item2, GetPayloads(segment)))));
            }

            throw new NotSupportedException($"Message {message.Key} is not supported.");
        }

        private async Task PublishCommandAsync(Message message)
        {
            var command = _serializer.Parse<Command>(message.Value, message.Encoding);
            switch (command.CommandType)
            {
                case CommandType.AddToGroup:
                    {
                        // name is hg-{HubName}.{GroupName}, consider the whole as the actual group name
                        // With multiple hubs, every hub connection receives the JoinGroupMessage
                        var name = command.Value;
                        var connectionId = GetName(message.Key, PrefixHelper.ConnectionIdPrefix);

                        await _serviceConnectionManager.WriteAsync(new JoinGroupMessage(connectionId, name));
                        _ackHandler.TriggerAck(message.CommandId);
                    }
                    break;
                case CommandType.RemoveFromGroup:
                    {
                        var name = command.Value;
                        var connectionId = GetName(message.Key, PrefixHelper.ConnectionIdPrefix);
                        await _serviceConnectionManager.WriteAsync(new LeaveGroupMessage(connectionId, name));
                    }
                    break;
                case CommandType.Initializing:
                    break;
                case CommandType.Abort:
                    break;
            }
        }

        private ReadOnlyMemory<byte> GetPayload(Message message)
        {
            IJsonWritable value = new PersistentResponse(m => false, tw => tw.Write("Cursor"))
            {
                Messages = new List<ArraySegment<Message>>
                {
                    new ArraySegment<Message>(new[] {message})
                },
                TotalCount = 1
            };

            using (var writer = new MemoryPoolTextWriter(_pool))
            {
                value.WriteJson(writer);
                writer.Flush();

                // Reuse ConnectionDataMessage to wrap the payload
                return _serviceProtocol.GetMessageBytes(new ConnectionDataMessage(string.Empty, writer.Buffer));
            }
        }

        private IReadOnlyList<string> GetExcludedIds(string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return null;
            }

            return filter.Split('|').Select(s => GetName(s, PrefixHelper.ConnectionIdPrefix)).ToArray();
        }

        private static Dictionary<string, ReadOnlyMemory<byte>> GetPayloads(ReadOnlyMemory<byte> data)
        {
            return new Dictionary<string, ReadOnlyMemory<byte>>
            {
                { "json", data }
            };
        }

        private static bool TryGetName(string qualifiedName, string prefix, out string name)
        {
            if (qualifiedName.StartsWith(prefix))
            {
                name = qualifiedName.Substring(prefix.Length);
                return true;
            }

            name = default;
            return false;
        }

        private static string GetName(string qualifiedName, string prefix)
        {
            if (TryGetName(qualifiedName, prefix, out var name))
            {
                return name;
            }

            throw new InvalidDataException($"Invalid name: {qualifiedName} does not start with {prefix}.");
        }
    }
}
