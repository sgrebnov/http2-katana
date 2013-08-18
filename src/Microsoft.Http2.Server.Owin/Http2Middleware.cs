using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Http2.Server.Owin
{
    // Http-01/2.0 uses a similar upgrade handshake to WebSockets. This middleware answers upgrade requests
    // using the Opaque Upgrade OWIN extension and then switches the pipeline to HTTP/2.0 binary framing.
    // Interestingly the HTTP/2.0 handshake does not need to be the first HTTP/1.1 request on a connection, only the last.
    public class Http2Middleware
    {
        // Pass requests onto this pipeline if not upgrading to HTTP/2.0.
        private readonly Func<IDictionary<string, object>, Task> _next;
        // Pass requests onto this pipeline if upgraded to HTTP/2.0.
        private Func<IDictionary<string, object>, Task> _nextHttp2;

        public Http2Middleware(Func<IDictionary<string, object>, Task> next)
        {
            _next = next;
            _nextHttp2 = _next;
        }

        public Http2Middleware(Func<IDictionary<string, object>, Task> next, Func<IDictionary<string, object>, Task> branch)
        {
            _next = next;
            _nextHttp2 = branch;
        }

        /// <summary>
        /// Invokes the specified environment.
        /// This method is used for handshake.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <returns></returns>
        public Task Invoke(IDictionary<string, object> environment)
        {
            if (environment["HandshakeAction"] is Func<Task>)
            {
                var handshakeAction = (Func<Task>)environment["HandshakeAction"];
                return handshakeAction.Invoke();
            }
            return _next.Invoke(environment);
        }
    }
}
