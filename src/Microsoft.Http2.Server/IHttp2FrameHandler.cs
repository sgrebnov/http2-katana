using System;
using SharedProtocol;

namespace SocketServer
{
    public interface IHttp2FrameHandler: IDisposable
    {
        void ProcessFrame(object sender, FrameReceivedEventArgs args);
    }
}
