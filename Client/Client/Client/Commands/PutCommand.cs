using System;
using System.Configuration;
using System.IO;

namespace Client.Commands
{
    internal sealed class PutCommand : Command, IUriCommand
    {
        private Uri _uri;
        private readonly string _method;

        public Uri Uri
        {
            get { return _uri; }
        }

        public string Path { get { return _uri.PathAndQuery; } }
        public string Method { get { return _method; } }
        public string LocalPath { get; private set; }

        internal PutCommand(string cmdBody)
        {
            _method = "put";
            Parse(cmdBody);
        }

        protected override void Parse(string cmd)
        {
            if (!cmd.Substring(7, cmd.IndexOf('/', 7) - 7).Contains(":"))
                throw new InvalidOperationException("Specify the port!");

            if (Uri.TryCreate(cmd, UriKind.Absolute, out _uri) == false)
            {
                throw new InvalidOperationException("Invalid Get command!");
            }

            int securePort;

            try
            {
                securePort = int.Parse(ConfigurationManager.AppSettings["securePort"]);
            }
            catch (Exception)
            {
                throw new InvalidOperationException("Invalid port in the config file");
            }

            if (Uri.Port == securePort
                &&
                Uri.Scheme == Uri.UriSchemeHttp
                ||
                Uri.Port != securePort
                &&
                Uri.Scheme == Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException("Invalid scheme on port! Use https for secure port");
            }

            var localPathPosition = cmd.IndexOf("\\", StringComparison.Ordinal) - 2;
            var localPath = cmd.Substring(localPathPosition, cmd.IndexOf('-') - localPathPosition);

            if (!File.Exists(localPath))
            {
                throw new FileNotFoundException("The file " + localPath + " doesn't exist!");
            }

            LocalPath = localPath;
        }

        internal override CommandType GetCmdType()
        {
            return CommandType.Post;
        }
    }
}
