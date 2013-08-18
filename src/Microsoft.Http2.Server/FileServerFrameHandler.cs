using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Org.Mentalis.Security.Ssl;
using SharedProtocol;
using SharedProtocol.Extensions;
using SharedProtocol.Framing;
using SharedProtocol.IO;
using SharedProtocol.Utils;

namespace SocketServer
{
    public class FileServerFrameHandler : IHttp2FrameHandler
    {
        private readonly FileHelper _fileHelper = new FileHelper(ConnectionEnd.Server);
        private readonly object _writeLock = new object();
        private List<string> _listOfRootFiles = new List<string>();
        private readonly object _listWriteLock = new object();
        private const string IndexFileName = @"\index.html";

        private const string IndexHtml = "\\index.html";
        private const string Root = "\\Root";
        private static readonly string AssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase.Substring(8));

        public FileServerFrameHandler()
        {
            InitializeRootFileList();
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
                            SaveDataFrame(stream, (DataFrame)args.Frame);
                            //Avoid leading \ at the filename
                            AddFileToRootFileList(stream.Headers.GetValue(":path").Substring(1));
                            break;
                    }
                }
                else if (args.Frame is HeadersFrame)
                {
                    switch (method)
                    {
                        case "get":
                        case "dir":
                            try
                            {
                                string path = stream.Headers.GetValue(":path").Trim('/');
                                SendFile(path, stream);
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

        private void AddFileToRootFileList(string fileName)
        {
            lock (_listWriteLock)
            {
                //if file is located in root directory then add it to the index
                if (!_listOfRootFiles.Contains(fileName) && !fileName.Contains("/"))
                {
                    using (var indexFile = new StreamWriter(AssemblyPath + Root + IndexHtml, true))
                    {
                        _listOfRootFiles.Add(fileName);
                        indexFile.Write(fileName + "<br>\n");
                    }
                }
            }
        }

        private void InitializeRootFileList()
        {
            lock (IndexFileName)
            {
                using (var indexFile = new StreamWriter(AssemblyPath + @"\Root" + IndexFileName))
                {
                    string dirPath = AssemblyPath + @"\Root";
                    _listOfRootFiles =
                        Directory.EnumerateFiles(dirPath, "*", SearchOption.TopDirectoryOnly)
                                 .Select(Path.GetFileName)
                                 .ToList();
                    foreach (var fileName in _listOfRootFiles)
                    {
                        indexFile.Write(fileName + "<br>\n");
                    }
                }
            }
        }

        private void SaveDataFrame(Http2Stream stream, DataFrame dataFrame)
        {
            lock (_writeLock)
            {
                string path = stream.Headers.GetValue(":path".ToLower());

                try
                {
                    string pathToSave = AssemblyPath + Root + path;
                    if (!Directory.Exists(Path.GetDirectoryName(pathToSave)))
                    {
                        throw new DirectoryNotFoundException("Access denied");
                    }
                    _fileHelper.SaveToFile(dataFrame.Data.Array, dataFrame.Data.Offset, dataFrame.Data.Count,
                                              pathToSave, stream.ReceivedDataAmount != 0);
                }
                catch (Exception ex)
                {
                    Http2Logger.LogError(ex.Message);
                    stream.WriteDataFrame(new byte[0], true);
                    stream.Dispose();
                }

                stream.ReceivedDataAmount += dataFrame.FrameLength;

                if (dataFrame.IsEndStream)
                {
                    if (!stream.EndStreamSent)
                    {
                        //send terminator
                        stream.WriteDataFrame(new byte[0], true);
                        Http2Logger.LogDebug("Terminator was sent");
                    }
                    _fileHelper.RemoveStream(AssemblyPath + Root + path);
                }
            }
        }

        // TODO move to http2protocol (extension method to Http2Stream??)
        private void SendFile(string path, Http2Stream stream)
        {
            // check if root is requested, in which case send index.html
            if (string.IsNullOrEmpty(path))
                path = IndexHtml;

            byte[] binary = _fileHelper.GetFile(path);
            WriteStatus(stream, StatusCode.Code200Ok, false);
            SendDataTo(stream, binary);
            Http2Logger.LogDebug("File sent: " + path);
        }

        private void WriteStatus(Http2Stream stream, int statusCode, bool final)
        {
            var headers = new HeadersList
            {
                new KeyValuePair<string, string>(":status", statusCode.ToString()),
            };
            stream.WriteHeadersFrame(headers, final);
        }

        public void Dispose()
        {
            _fileHelper.Dispose();
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

    }
}
