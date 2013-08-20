using System;
using SharedProtocol;
using SharedProtocol.EventArgs;

namespace SocketServer
{
    public interface IHttp2FrameHandler: IDisposable
    {
        void ProcessFrame(object sender, FrameReceivedEventArgs args);
    }
}
