// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Json;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR;
using Microsoft.Azure.SignalR.Protocol;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.AspNet.SignalR
{
    internal class ServiceMessageBus : MessageBus
    {
        private const string HubPrefix = "h-";
        private const string HubGroupPrefix = "hg-";
        private const string HubConnectionIdPrefix = "hc-";
        private const string HubUserPrefix = "hu-";

        private const string PersistentConnectionPrefix = "pc-";
        private const string PersistentConnectionGroupPrefix = "pcg-";

        private const string ConnectionIdPrefix = "c-";

        private readonly JsonSerializer _serializer;

        private readonly IServiceConnectionManager _serviceConnectionManager;

        private readonly IAckHandler _ackHandler;

        public ServiceMessageBus(IDependencyResolver resolver) : base(resolver)
        {
            _serviceConnectionManager = resolver.Resolve<IServiceConnectionManager>();
            _serializer = resolver.Resolve<JsonSerializer>();
            _ackHandler = resolver.Resolve<IAckHandler>();
        }

        public override async Task Publish(Message message)
        {
            Dictionary<string, ReadOnlyMemory<byte>> GetPayload(ReadOnlyMemory<byte> data) =>
                new Dictionary<string, ReadOnlyMemory<byte>>
                {
                    {"json", data }
                };

            var response = new PersistentResponse(m => false, tw => tw.Write("Cursor"))
            {
                Messages = new List<ArraySegment<Message>>
                {
                    new ArraySegment<Message>(new[] {message})
                },
                TotalCount = 1
            };

            // TODO: use MemoryPoolTextWriter
            ArraySegment<byte> segment;
            var ms = new MemoryStream();
            using (var sw = new StreamWriter(ms))
            {
                ((IJsonWritable)response).WriteJson(sw);
                sw.Flush();
                ms.TryGetBuffer(out segment);
            }

            if (message.IsCommand)
            {
                var command = _serializer.Parse<Command>(message.Value, message.Encoding);
                switch (command.CommandType)
                {
                    case CommandType.AddToGroup:
                        {
                            // name is hg-{HubName}.{GroupName}, consider the whole as the actual group name
                            var name = command.Value;

                            if (message.Key.StartsWith(ConnectionIdPrefix))
                            {
                                var connectionId = message.Key.Substring(ConnectionIdPrefix.Length);

                                // Hub name can have '.', and group name can have '.', is there a way to determine which hub this group is in?
                                await _serviceConnectionManager.WriteAsync(new JoinGroupMessage(connectionId, name));
                                _ackHandler.TriggerAck(message.CommandId);
                            }
                        }
                        break;
                    case CommandType.RemoveFromGroup:
                        {
                            var name = command.Value;

                            if (message.Key.StartsWith(ConnectionIdPrefix))
                            {
                                var connectionId = message.Key.Substring(ConnectionIdPrefix.Length);

                                await _serviceConnectionManager.WriteAsync(new LeaveGroupMessage(connectionId, name));
                            }
                        }
                        break;
                    case CommandType.Initializing:
                        break;
                    case CommandType.Abort:
                        break;
                }
            }
            else
            {
                // broadcast case
                if (message.Key.StartsWith(HubPrefix))
                {
                    var hubName = message.Key.Substring(HubPrefix.Length);
                    await _serviceConnectionManager.WithHub(hubName).WriteAsync(new BroadcastDataMessage(excludedList: GetExcludedIds(message.Filter), payloads: GetPayload(segment)));
                }
                // echo case
                if (message.Key.StartsWith(HubConnectionIdPrefix))
                {
                    var hubAndConnectionId = message.Key.Substring(HubConnectionIdPrefix.Length);

                    // ConnectionID is base64 encoded, it does not contain '.'
                    var index = hubAndConnectionId.LastIndexOf('.');
                    var hub = hubAndConnectionId.Substring(0, index);
                    var connectionId = hubAndConnectionId.Substring(index + 1);
                    await _serviceConnectionManager.WithHub(hub).WriteAsync(new ConnectionDataMessage(connectionId, segment));
                }
                else if (message.Key.StartsWith(HubGroupPrefix))
                {
                    await _serviceConnectionManager.WriteAsync(new GroupBroadcastDataMessage(message.Key, excludedList: GetExcludedIds(message.Filter), payloads: GetPayload(segment)));
                }
                else if (message.Key.StartsWith(ConnectionIdPrefix))
                {
                    await _serviceConnectionManager.WriteAsync(new ConnectionDataMessage(message.Key.Substring(ConnectionIdPrefix.Length), segment));
                }
            }
        }

        private IReadOnlyList<string> GetExcludedIds(string filter)
        {
            return filter?.Split('|').Select(GetConnectionId).Where(s => s != null).ToArray();
        }

        private string GetConnectionId(string prefixedId)
        {
            if (prefixedId.StartsWith(ConnectionIdPrefix))
            {
                return prefixedId.Substring(ConnectionIdPrefix.Length);
            }

            // TODO: what to do for invalid data?
            // throw new InvalidDataException($"connection id should start with {ConnectionIdPrefix} however it is {prefixedId}");
            return null;
        }
    }
}
