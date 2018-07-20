// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Json;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.Protocol;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.AspNet
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

        private readonly IServiceProtocol _serviceProtocol;

        public ServiceMessageBus(IDependencyResolver resolver) : base(resolver)
        {
            _serviceConnectionManager = resolver.Resolve<IServiceConnectionManager>();
            _serializer = resolver.Resolve<JsonSerializer>();
            _ackHandler = resolver.Resolve<IAckHandler>();
            _serviceProtocol = resolver.Resolve<IServiceProtocol>();
        }

        public override async Task Publish(Message message)
        {
            Dictionary<string, ReadOnlyMemory<byte>> GetPayloads(ReadOnlyMemory<byte> data) =>
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

            var segment = GetPayload(response);

            if (message.IsCommand)
            {
                var command = _serializer.Parse<Command>(message.Value, message.Encoding);
                switch (command.CommandType)
                {
                    case CommandType.AddToGroup:
                        {
                            // name is hg-{HubName}.{GroupName}, consider the whole as the actual group name
                            // What if multiple hubs? every hub connection receives the JoinGroupMessage
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
                    await _serviceConnectionManager.WithHub(hubName).WriteAsync(new BroadcastDataMessage(excludedList: GetExcludedIds(message.Filter), payloads: GetPayloads(segment)));
                }
                // echo case
                else if (message.Key.StartsWith(HubConnectionIdPrefix))
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
                    await _serviceConnectionManager.WriteAsync(new GroupBroadcastDataMessage(message.Key, excludedList: GetExcludedIds(message.Filter), payloads: GetPayloads(segment)));
                }
                else if (message.Key.StartsWith(ConnectionIdPrefix))
                {
                    await _serviceConnectionManager.WriteAsync(new ConnectionDataMessage(message.Key.Substring(ConnectionIdPrefix.Length), segment));
                }
                else if (message.Key.StartsWith(HubUserPrefix))
                {
                    // naming: hu-{HubName}.{UserName} 
                    // HubName can contain '.' and UserName can contain '.'
                    // How to map it to the User?
                    // Currently we go through all the possibilities
                    var result = GetPossibleConnections(message.Key.Substring(HubUserPrefix.Length));
                    foreach(var pair in result)
                    {
                        // For old protocol, it is always single user per message https://github.com/SignalR/SignalR/blob/dev/src/Microsoft.AspNet.SignalR.Core/Infrastructure/Connection.cs#L162
                        await _serviceConnectionManager.WithHub(pair.Item1).WriteAsync(new UserDataMessage(pair.Item2, GetPayloads(segment)));
                    }
                }
            }
        }

        private ReadOnlyMemory<byte> GetPayload(IJsonWritable value)
        {
            // TODO: use MemoryPoolTextWriter
            ArraySegment<byte> segment;
            var ms = new MemoryStream();
            using (var sw = new StreamWriter(ms))
            {
                value.WriteJson(sw);
                sw.Flush();
                ms.TryGetBuffer(out segment);
            }

            // Reuse ConnectionDataMessage to wrap the payload
            var message = new ConnectionDataMessage(string.Empty, segment);
            return _serviceProtocol.GetMessageBytes(message);
        }

        /// <summary>
        /// The qualified name is {HubName}.{OtherName} while both can contain '.', go through every possiblity
        /// </summary>
        /// <param name="candidate"></param>
        /// <returns></returns>
        private IEnumerable<(string, string)> GetPossibleConnections(string candidate)
        {
            var index = candidate.IndexOf('.');
            if (index == -1)
            {
                throw new InvalidDataException($"Message key {candidate} does not contain the required separator '.'");
            }

            // It is rare that hubname contains '.'
            foreach (var name in _serviceConnectionManager.HubNamesWithDot)
            {
                if (candidate.Length > name.Length + 1 && candidate[name.Length] == '.')
                {
                    yield return (name, candidate.Substring(name.Length + 1));
                }
            }

            yield return (candidate.Substring(0, index), candidate.Substring(index + 1));
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
