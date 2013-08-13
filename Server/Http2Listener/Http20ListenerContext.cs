using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace Owin.Server.Http20Listener
{
    public sealed class Http20ListenerContext : IDisposable
    {
        private Http20ListenerRequest _request;

        internal Http20ListenerContext()
        {
            this._request = new Http20ListenerRequest();
        }

        public IPrincipal User
        {
            get
            {
                return null;
            }
        }

        public Http20ListenerRequest Request { get { return this._request; } }

        public void Dispose()
        {
           
        }
    }
}
