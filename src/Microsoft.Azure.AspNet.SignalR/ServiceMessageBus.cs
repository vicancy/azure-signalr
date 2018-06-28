// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Json;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR;
using Microsoft.Azure.SignalR.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
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

        private readonly IServiceConnectionManager _serviceConnectionManager;

        public ServiceMessageBus(IDependencyResolver resolver) : base(resolver)
        {
            _serviceConnectionManager = resolver.Resolve<IServiceConnectionManager>();
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

            // Which hub?
            if (message.Key.StartsWith(HubPrefix))
            {
                var hubName = message.Key.Substring(HubPrefix.Length);
                await _serviceConnectionManager.WithHub(hubName).WriteAsync(new BroadcastDataMessage(excludedList: null, payloads: GetPayload(segment)));
            }
            // Which group?
            else if (message.Key.StartsWith(HubGroupPrefix))
            {
                await _serviceConnectionManager.WriteAsync(new GroupBroadcastDataMessage(message.Key.Substring(HubGroupPrefix.Length), excludedList: null, payloads: GetPayload(segment)));
            }
            else if (message.Key.StartsWith(ConnectionIdPrefix))
            {
                await _serviceConnectionManager.WriteAsync(new ConnectionDataMessage(message.Key.Substring(ConnectionIdPrefix.Length), segment));
            }

            if (message.IsCommand)
            {
                // TODO: handle commands
                await base.Publish(message);
            }
        }
    }
}
