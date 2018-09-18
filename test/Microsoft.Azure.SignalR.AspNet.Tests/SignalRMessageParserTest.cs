// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Json;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.Azure.SignalR.Protocol;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class SignalRMessageParserTest
    {
        private readonly IDependencyResolver _resolver = GetDefaultResolver();
        private readonly MemoryPool _pool = new MemoryPool();
        private readonly JsonSerializer _serializer = new JsonSerializer();
        public void TestGetMessage()
        {
            var hubs = new List<string> { };
            var parser = new SignalRMessageParser(hubs, _resolver);
            var raw = "<script type=\"\"></script>";
            var message = new Message("foo", "key", new ArraySegment<byte>(Encoding.Default.GetBytes(raw)));

            var result = parser.GetMessages(message).ToList();
            

        }

        private Message CreateMessage(string key, object value)
        {
            ArraySegment<byte> messageBuffer = GetMessageBuffer(value);

            var message = new Message(_connectionId, key, messageBuffer);

            var command = value as Command;
            if (command != null)
            {
                // Set the command id
                message.CommandId = command.Id;
                message.WaitForAck = command.WaitForAck;
            }

            return message;
        }


        private ArraySegment<byte> GetMessageBuffer(object value)
        {
            ArraySegment<byte> messageBuffer;
            // We can't use "as" like we do for Command since ArraySegment is a struct
            if (value is ArraySegment<byte>)
            {
                // We assume that any ArraySegment<byte> is already JSON serialized
                messageBuffer = (ArraySegment<byte>)value;
            }
            else
            {
                messageBuffer = SerializeMessageValue(value);
            }
            return messageBuffer;
        }

        private ArraySegment<byte> SerializeMessageValue(object value)
        {
            using (var writer = new MemoryPoolTextWriter(_pool))
            {

                var selfSerializer = value as IJsonWritable;

                if (selfSerializer != null)
                {
                    selfSerializer.WriteJson(writer);
                }
                else
                {
                    _serializer.Serialize(writer, value);
                }

                writer.Flush();

                var data = writer.Buffer;

                var buffer = new byte[data.Count];

                Buffer.BlockCopy(data.Array, data.Offset, buffer, 0, data.Count);

                return new ArraySegment<byte>(buffer);
            }
        }

        private static IDependencyResolver GetDefaultResolver()
        {
            var config = new HubConfiguration();
            var resolver = config.Resolver;
            resolver.Register(typeof(JsonSerializer), () => new JsonSerializer());
            resolver.Register(typeof(IServiceProtocol), () => new ServiceProtocol());
            resolver.Register(typeof(IMemoryPool), () => new MemoryPool());
            return resolver;
        }
    }
}