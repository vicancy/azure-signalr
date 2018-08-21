// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.Azure.SignalR.Protocol;
using Xunit;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class ServiceMessageBusTests
    {
        [Fact]
        public async Task PublishInvalidMessageThrows()
        {
            var dr = GetDefaultResolver(out _);
            using (var bus = new ServiceMessageBus(dr))
            {
                await Assert.ThrowsAsync<NotSupportedException>(() => bus.Publish("test", "key", "1"));
            }
        }

        [Fact]
        public async Task PublishMessageToNotExistHubThrows()
        {
            var dr = GetDefaultResolver(out _);
            using (var bus = new ServiceMessageBus(dr))
            {
                await Assert.ThrowsAsync<KeyNotFoundException>(() => bus.Publish("test", "h-key", "1"));
            }
        }

        public static IEnumerable<object[]> BroadcastTestMessages => new object[][]
            {
                // hub connection "c1" gets this connection message
                new object[]
                {
                    "h-c1", "hello", new string[] { "h-c1", "h", "c1" }, new string[] { "c1" }
                },
                // hub connection "a.b" gets this connection message
                new object[]
                {
                    "h-a.b", "hello", new string[] { "h-c1", "h", "a.b" }, new string[] { "a.b" },
                }
            };

        [Theory]
        [MemberData(nameof(BroadcastTestMessages))]
        public async Task PublishBroadcastMessagesTest(string messageKey, string messageValue, string[] availableHubs, string[] expectedHubs)
        {
            var dr = GetDefaultResolver(out var scm);

            PrepareConnection(scm, availableHubs, out var result);

            using (var bus = new ServiceMessageBus(dr))
            {
                await bus.Publish("test", messageKey, messageValue);
            }

            Assert.Equal(expectedHubs.Length, result.Count);

            for (var i = 0; i < expectedHubs.Length; i++)
            {
                Assert.True(result.TryGetValue(expectedHubs[i], out var current));
                var message = current as BroadcastDataMessage;
                Assert.NotNull(message);
                Assert.Equal($"{{\"C\":\"Cursor\",\"M\":[{messageValue}]}}", GetSingleFramePayload(message.Payloads["json"]));
            }
        }

        public static IEnumerable<object[]> ConnectionDataTestMessages => new object[][]
            {
                // hub connection "hub1" gets this connection message
                new object[]
                {
                    "hc-hub1.c1", "hello", new string[] { "hc", "hc-", "hub1", "hub1.c", "hub1.c1" }, new string[] { "hub1" }, new string[] { "c1" }
                },
                // hub connection "hub1" & "hub1.bi" gets this connection message
                new object[]
                {
                    "hc-hub1.bi.conn1", "hello", new string[] { "hc", "hc-hub1", "hub1", "hub1.bi", "hub1.bi.conn" }, new string[] { "hub1", "hub1.bi" }, new string[] { "bi.conn1", "conn1" }
                }
            };

        [Theory]
        [MemberData(nameof(ConnectionDataTestMessages))]
        public async Task PublishConnectionDataMessagesTest(string messageKey, string messageValue, string[] availableHubs, string[] expectedHubs, string[] expectedConnectionIds)
        {
            var dr = GetDefaultResolver(out var scm);

            PrepareConnection(scm, availableHubs, out var result);

            using (var bus = new ServiceMessageBus(dr))
            {
                await bus.Publish("test", messageKey, messageValue);
            }

            Assert.Equal(expectedHubs.Length, result.Count);
            Assert.Equal(expectedConnectionIds.Length, result.Count);

            for (var i = 0; i < expectedHubs.Length; i++)
            {
                Assert.True(result.TryGetValue(expectedHubs[i], out var current));
                var message = current as ConnectionDataMessage;
                Assert.NotNull(message);

                Assert.Equal(expectedConnectionIds[i], message.ConnectionId);
                Assert.Equal($"{{\"C\":\"Cursor\",\"M\":[{messageValue}]}}", GetSingleFramePayload(message.Payload.First));
            }
        }

        public static IEnumerable<object[]> GroupBroadcastTestMessages => new object[][]
            {
                // hub connection "h1" gets this connection message
                new object[]
                {
                    // For groups, group name as a whole is considered as the group name
                    "hg-h1.group1", "hello", new string[] { "hg-h1", "hg", "h1" }, new string[] { "h1" }, new string[] { "hg-h1.group1" }
                },
                // hub connection "h1" & "h1.a1" gets this connection message, with group name not changed
                new object[]
                {
                    "hg-h1.a1.group1", "hello", new string[] { "hg-h1", "hg", "h1", "h1.a1" }, new string[] { "h1", "h1.a1" }, new string[] { "hg-h1.a1.group1", "hg-h1.a1.group1" }
                }
            };

        [Theory]
        [MemberData(nameof(GroupBroadcastTestMessages))]
        public async Task PublishGroupBroadcastDataMessagesTest(string messageKey, string messageValue, string[] availableHubs, string[] expectedHubs, string[] expectedGroups)
        {
            var dr = GetDefaultResolver(out var scm);

            PrepareConnection(scm, availableHubs, out var result);

            using (var bus = new ServiceMessageBus(dr))
            {
                await bus.Publish("test", messageKey, messageValue);
            }

            Assert.Equal(expectedHubs.Length, result.Count);
            Assert.Equal(expectedGroups.Length, result.Count);

            for (var i = 0; i < expectedHubs.Length; i++)
            {
                Assert.True(result.TryGetValue(expectedHubs[i], out var current));
                var message = current as GroupBroadcastDataMessage;
                Assert.NotNull(message);
                Assert.Equal(message.GroupName, expectedGroups[i]);
                Assert.Equal($"{{\"C\":\"Cursor\",\"M\":[{messageValue}]}}", GetSingleFramePayload(message.Payloads["json"]));
            }
        }

        public static IEnumerable<object[]> UserDataTestMessages => new object[][]
            {
                // hub connection "hub1" gets this connection message
                new object[]
                {
                    "hu-hub1.user1", "hello", new string[] { "hu", "hu-", "hub1", "hub1.u", "hub1.user1" }, new string[] { "hub1" }, new string[] { "user1" }
                },
                // hub connection "hub1" & "hub1.bi" gets this connection message
                new object[]
                {
                    "hu-hub1.bi.user1", "hello", new string[] { "hu", "hu-hub1", "hub1", "hub1.bi", "hub1.bi.user" }, new string[] { "hub1", "hub1.bi" }, new string[] { "bi.user1", "user1" }
                }
            };

        [Theory]
        [MemberData(nameof(UserDataTestMessages))]
        public async Task PublishUserDataMessagesTest(string messageKey, string messageValue, string[] availableHubs, string[] expectedHubs, string[] expectedUsers)
        {
            var dr = GetDefaultResolver(out var scm);

            PrepareConnection(scm, availableHubs, out var result);

            using (var bus = new ServiceMessageBus(dr))
            {
                await bus.Publish("test", messageKey, messageValue);
            }

            Assert.Equal(expectedHubs.Length, result.Count);
            Assert.Equal(expectedUsers.Length, result.Count);

            for (var i = 0; i < expectedHubs.Length; i++)
            {
                Assert.True(result.TryGetValue(expectedHubs[i], out var current));
                var message = current as UserDataMessage;
                Assert.NotNull(message);

                Assert.Equal(expectedUsers[i], message.UserId);
                Assert.Equal($"{{\"C\":\"Cursor\",\"M\":[{messageValue}]}}", GetSingleFramePayload(message.Payloads["json"]));
            }
        }

        private static void PrepareConnection(IServiceConnectionManager scm, IEnumerable<string> hubs, out SortedList<string, ServiceMessage> output)
        {
            var result = new SortedList<string, ServiceMessage>();
            foreach (var hub in hubs)
            {
                scm.AddConnection(hub, new TestServiceConnection(hub,
                    m =>
                    {
                        lock (result)
                        {
                            result.Add(hub, m.Item1);
                        }
                    }));
            }
            output = result;
        }

        private static IDependencyResolver GetDefaultResolver(out IServiceConnectionManager scm)
        {
            var resolver = new DefaultDependencyResolver();
            resolver.Register(typeof(IServiceProtocol), () => new ServiceProtocol());
            var connectionManager = new ServiceConnectionManager();
            resolver.Register(typeof(IServiceConnectionManager), () => connectionManager);
            scm = connectionManager;
            return resolver;
        }

        private sealed class TestServiceConnection : IServiceConnection
        {
            private readonly Action<(ServiceMessage, IServiceConnection)> _validator;

            public string HubName { get; }

            public TestServiceConnection(string name, Action<(ServiceMessage, IServiceConnection)> validator)
            {
                _validator = validator;
                HubName = name;
            }

            public Task StartAsync()
            {
                return Task.CompletedTask;
            }

            public Task WriteAsync(ServiceMessage serviceMessage)
            {
                _validator((serviceMessage, this));
                return Task.CompletedTask;
            }

            public Task StopAsync()
            {
                return Task.CompletedTask;
            }
        }

        private static IServiceProtocol DefaultServiceProtocol = new ServiceProtocol();

        private static ReadOnlyMemory<byte> GenerateSingleFrameBuffer(ReadOnlyMemory<byte> inner)
        {
            var singleFrameMessage = new ConnectionDataMessage(string.Empty, inner);
            return DefaultServiceProtocol.GetMessageBytes(singleFrameMessage);
        }

        private static ReadOnlyMemory<byte> GenerateSingleFrameBuffer(string message)
        {
            var inner = Encoding.UTF8.GetBytes(message);
            var singleFrameMessage = new ConnectionDataMessage(string.Empty, inner);
            return DefaultServiceProtocol.GetMessageBytes(singleFrameMessage);
        }

        private static string GetSingleFramePayload(ReadOnlyMemory<byte> payload)
        {
            var buffer = new ReadOnlySequence<byte>(payload);
            DefaultServiceProtocol.TryParseMessage(ref buffer, out var message);
            var frame = message as ConnectionDataMessage;
            Assert.NotNull(frame);
            return Encoding.UTF8.GetString(frame.Payload.First.ToArray());
        }
    }
}