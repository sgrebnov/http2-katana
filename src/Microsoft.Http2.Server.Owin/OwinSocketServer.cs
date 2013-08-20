using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Org.Mentalis.Security.Certificates;
using Org.Mentalis.Security.Ssl;
using Org.Mentalis.Security.Ssl.Shared.Extensions;
using System.Configuration;
using SharedProtocol;
using SharedProtocol.EventArgs;
using SharedProtocol.Utils;

namespace SocketServer
{
    using AppFunc = Func<IDictionary<string, object>, Task>;
    using SharedProtocol.Framing;
    using System.Text;
    using SharedProtocol.Extensions;
    using Org.Mentalis;
    using SharedProtocol.Handshake;
    using SharedProtocol.Exceptions;

    /// <summary>
    /// Http2 socket server implementation that uses raw socket.
    /// </summary>
    public class OwinSocketServer : Http2ServerBase
    {
        private AppFunc _next;
        private static readonly string AssemblyName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase.Substring(8));
       

        public OwinSocketServer(Func<IDictionary<string, object>, Task> next, IDictionary<string, object> properties) :
            base(properties)
        {
            _next = next;
            Listen();
        }

        private static Dictionary<string, object> CreateOwinEnvironment(string method, string scheme, string hostHeaderValue, string pathBase, string path, byte[] requestBody = null)
        {
            var environment = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            environment["owin.RequestMethod"] = method;
            environment["owin.RequestScheme"] = scheme;
            environment["owin.RequestHeaders"] = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { { "Host", new string[] { hostHeaderValue } } };
            environment["owin.RequestPathBase"] = pathBase;
            environment["owin.RequestPath"] = path;
            environment["owin.RequestQueryString"] = "";
            environment["owin.RequestBody"] = new MemoryStream(requestBody ?? new byte[0]);
            environment["owin.CallCancelled"] = new CancellationToken();
            environment["owin.ResponseHeaders"] = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            environment["owin.ResponseBody"] = new MemoryStream();
            return environment;
        }

        public override void ProcessFrame(object sender, FrameReceivedEventArgs args)
        {
            var stream = args.Stream;
            var method = stream.Headers.GetValue(":method");
            if (!string.IsNullOrEmpty(method))
                method = method.ToLower();

            try
            {
                if (args.Frame is DataFrame)
                {
                    switch (method)
                    {
                        case "post":
                        case "put":
                            HandlePost(sender as Http2Session, args.Frame, stream);
                            break;
                    }
                }
                else if (args.Frame is HeadersFrame)
                {
                    switch (method)
                    {
                        case "get":
                        case "delete":
                            HandleGet(sender as Http2Session, stream);
                            break;

                        default:
                            Http2Logger.LogDebug("Received headers with Status: " + stream.Headers.GetValue(":status"));
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Http2Logger.LogDebug("Error: " + e.Message);
                stream.WriteRst(ResetStatusCode.InternalError);
                stream.Dispose();
            }
        }

        private void HandleGet(Http2Session session, Http2Stream stream)
        {
            string path = stream.Headers.GetValue(":path");
            string scheme = stream.Headers.GetValue(":scheme");
            string method = stream.Headers.GetValue(":method");
            string host = stream.Headers.GetValue(":host") + ":" + (session.Socket.LocalEndPoint as System.Net.IPEndPoint).Port;

            Http2Logger.LogDebug(method.ToUpper() + ": " + path);

            var env = CreateOwinEnvironment(method.ToUpper(), scheme, host, "", path);
            env["HandshakeAction"] = null;

            _next(env).ContinueWith(r =>
            {
                try
                {

                    var bytes = (env["owin.ResponseBody"] as MemoryStream).ToArray();

                    string res = Encoding.UTF8.GetString(bytes);

                    Console.WriteLine("Done: " + res);

                    SendDataTo(stream, bytes);
                }
                catch (Exception ex)
                {
                    Http2Logger.LogError("Error: " + ex.Message);
                }
            });
        }

        private void HandlePost(Http2Session session, Frame frame, Http2Stream stream)
        {
            string path = stream.Headers.GetValue(":path");
            string scheme = stream.Headers.GetValue(":scheme");
            string method = stream.Headers.GetValue(":method");
            string host = stream.Headers.GetValue(":host") + ":" + (session.Socket.LocalEndPoint as System.Net.IPEndPoint).Port;
            byte[] body = new byte[frame.Payload.Count];
            Array.ConstrainedCopy(frame.Payload.Array, frame.Payload.Offset, body, 0, body.Length);

            Http2Logger.LogDebug(method.ToUpper() + ": " + path);

            var env = CreateOwinEnvironment(method.ToUpper(), scheme, host, "", path, frame.FrameType == FrameType.Data ? body : null);
            env["HandshakeAction"] = null;
            (env["owin.RequestHeaders"] as Dictionary<string, string[]>).Add("Content-Type", new string[] { stream.Headers.GetValue(":content-type") });

            _next(env).ContinueWith(r =>
            {
                try
                {

                    var bytes = (env["owin.ResponseBody"] as MemoryStream).ToArray();

                    string res = Encoding.UTF8.GetString(bytes);

                    Console.WriteLine("Done: " + res);

                    SendDataTo(stream, bytes);
                }
                catch (Exception ex)
                {
                    Http2Logger.LogError("Error: " + ex.Message);
                }
            });
        }

        // TODO move to http2protocol (extension method to Http2Stream??)
        private void SendDataTo(Http2Stream stream, byte[] binaryData)
        {
            int i = 0;

            Http2Logger.LogDebug("Transfer begin");

            do
            {
                bool isLastData = binaryData.Length - i < Constants.MaxDataFrameContentSize;

                int chunkSize = stream.WindowSize > 0
                                    ? MathEx.Min(binaryData.Length - i, Constants.MaxDataFrameContentSize,
                                                 stream.WindowSize)
                                    : MathEx.Min(binaryData.Length - i, Constants.MaxDataFrameContentSize);

                var chunk = new byte[chunkSize];
                Buffer.BlockCopy(binaryData, i, chunk, 0, chunk.Length);

                stream.WriteDataFrame(chunk, isLastData);

                i += chunkSize;
            } while (binaryData.Length > i);
        }

        public override string HandleHandshake(IDictionary<string, object> handshakeEnvironment)
        {
            IDictionary<string, object> handshakeResult = null;
            Func<Task> handshakeAction = () =>
            {
                var handshakeTask = new Task(() =>
                {
                    handshakeResult = HandshakeManager.GetHandshakeAction(handshakeEnvironment).Invoke();
                });
                return handshakeTask;
            };

            var environment = new Dictionary<string, object>
                    {
                        //Sets the handshake action depends on port.
                        {"HandshakeAction", handshakeAction},
                    };

            try
            {
                var handshakeTask = _next(environment);

                handshakeTask.Start();
                if (!handshakeTask.Wait(6000))
                {
                    Http2Logger.LogError("Handshake timeout. Connection dropped.");
                    return null;
                }

                return handshakeEnvironment.Get<SecureSocket>("secureSocket").SelectedProtocol;
            }
            catch (Http2HandshakeFailed ex)
            {
                if (ex.Reason == HandshakeFailureReason.InternalError)
                {
                    return Protocols.Http1;
                }
                else
                {
                    Http2Logger.LogError("Handshake timeout. Client was disconnected.");
                    return null;
                }
            }
            catch (Exception e)
            {
                Http2Logger.LogError("Exception occured. Closing client's socket. " + e.Message);
                return null;
            }
        }
    }
}
