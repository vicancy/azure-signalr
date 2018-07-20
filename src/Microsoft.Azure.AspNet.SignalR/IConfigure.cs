// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.Azure.SignalR.AspNet
{
    internal interface IConfigure<T> where T: class
    {
        T Value { get; }
    }
}
