using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Owin.Server.Http20Listener
{
    public sealed class Http20ListenerRequest
    {
        private static readonly Version _protocolVersion = new Version(2, 0);

        public Version ProtocolVersion
        {
            get { return Http20ListenerRequest._protocolVersion; }
        }
    }
}
