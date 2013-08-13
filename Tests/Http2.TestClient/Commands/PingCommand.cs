using System;

namespace Client.Commands
{
    internal sealed class PingCommand : Command
    {
        public Uri Uri { get; private set; }

        internal override CommandType GetCmdType()
        {
            return CommandType.Ping;
        }

        internal override void Parse(string[] cmdArgs)
        {
            if (cmdArgs == null)
            {
                throw  new ArgumentNullException("cmdArgs can't be null");
            }

            Uri uri;
            if (cmdArgs.Length == 0 || !Uri.TryCreate(cmdArgs[0], UriKind.Absolute, out uri))
            {
                throw new InvalidOperationException("Invalid ping format!");
            }
            Uri = uri;
        }
    }
}
