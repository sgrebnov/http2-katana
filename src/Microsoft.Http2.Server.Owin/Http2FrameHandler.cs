using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharedProtocol;
using SharedProtocol.EventArgs;
using SharedProtocol.Extensions;
using SharedProtocol.Framing;
using SharedProtocol.Utils;
using SocketServer;


namespace Microsoft.Http2.Server.Owin
{
    internal class Http2FrameHandler : IHttp2FrameHandler
    {
        private Func<IDictionary<string, object>, Task> _next;

        public Http2FrameHandler(Func<IDictionary<string, object>, Task> next)
        {
            _next = next;
        }

        public void Dispose()
        {
            
        }

        public void ProcessFrame(object sender, FrameReceivedEventArgs args)
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
                            //SaveDataFrame(stream, (DataFrame)args.Frame);
                            //Avoid leading \ at the filename
                            //AddFileToRootFileList(stream.Headers.GetValue(":path").Substring(1));
                            break;
                    }
                }
                else if (args.Frame is HeadersFrame)
                {
                    switch (method)
                    {
                        case "get":
                            HandleGet(stream);
                            break;

                        case "dir":
                            try
                            {
                                string path = stream.Headers.GetValue(":path").Trim('/');
                                //SendFile(path, stream);
                            }
                            catch (FileNotFoundException e)
                            {
                                Http2Logger.LogDebug("File not found: " + e.FileName);
                                WriteStatus(stream, StatusCode.Code404NotFound, true);
                            }

                            break;
                        case "delete":
                            WriteStatus(stream, StatusCode.Code401Forbidden, true);
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

        private static Dictionary<string, object> CreateOwinEnvironment(string method, string scheme, string hostHeaderValue, string pathBase, string path)
        {
            var environment = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            environment["owin.RequestMethod"] = method;
            environment["owin.RequestScheme"] = scheme;
            environment["owin.RequestHeaders"] = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { { "Host", new string[] { hostHeaderValue } } };
            environment["owin.RequestPathBase"] = pathBase;
            environment["owin.RequestPath"] = path;
            environment["owin.RequestQueryString"] = "";
            environment["owin.RequestBody"] = new MemoryStream();
            environment["owin.CallCancelled"] = new CancellationToken();
            environment["owin.ResponseHeaders"] = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            environment["owin.ResponseBody"] = new MemoryStream();
            return environment;
        }

        private void HandleGet(Http2Stream stream)
        {

            string path = stream.Headers.GetValue(":path");

            Http2Logger.LogDebug("GET: " + path);


            var env = CreateOwinEnvironment("GET", "https", "localhost:8443", "", path);
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

        private void WriteStatus(Http2Stream stream, int statusCode, bool final)
        {
            var headers = new HeadersList
            {
                new KeyValuePair<string, string>(":status", statusCode.ToString()),
            };
            stream.WriteHeadersFrame(headers, final);
        }
    }
}
