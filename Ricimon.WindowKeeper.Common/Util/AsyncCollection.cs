using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ricimon.WindowKeeper.Common.Util
{
    public static class CancellableMethodCollection
    {
        private static IDictionary<object, CancellationTokenSource> _cancellationTokens = new Dictionary<object, CancellationTokenSource>();

        /// <summary>
        /// Provide a cancellable async method that can be called multiple times, and will cancel any ongoing calls that have the same key.
        /// </summary>
        /// <param name="key">Key to store the method call in</param>
        /// <param name="cancellableMethod">Method to call, supplying a token that will be cancelled if another method with the same key is called</param>
        public static void CallCancellableMethod(object key, Action<CancellationToken> cancellableMethod)
        {
            if (_cancellationTokens.TryGetValue(key, out var tokenSource))
            {
                tokenSource.Cancel();
                _cancellationTokens.Remove(key);
            }

            var cancelToken = new CancellationTokenSource();
            cancellableMethod?.Invoke(cancelToken.Token);
            _cancellationTokens.Add(key, cancelToken);
        }
    }
}
