// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.Azure.AspNet.SignalR
{
    internal class Configure<T> : IConfigure<T> where T : class
    {
        public Configure(T value)
        {
            Value = value;
        }

        public T Value { get; }
    }
}
