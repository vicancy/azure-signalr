// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace ChatSample.CoreApp3
{
    public class StreamingHub : Hub
    {
        private readonly IHubContext<Chat> _context;

        public StreamingHub(IHubContext<Chat> context)
        {
            _context = context;
        }

        public async IAsyncEnumerable<int> Counter(
            int count,
            int delay,
            [EnumeratorCancellation]
        CancellationToken cancellationToken)
        {
            for (var i = 0; i < count; i++)
            {
                // Check the cancellation token regularly so that the server will stop
                // producing items if the client disconnects.
                cancellationToken.ThrowIfCancellationRequested();

                yield return i;

                // Use the cancellationToken in other APIs that accept cancellation
                // tokens so the cancellation can flow down to them.
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}