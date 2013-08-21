﻿using SharedProtocol.Framing;

namespace SharedProtocol.EventArgs
{
    /// <summary>
    /// This class is designed for future usage
    /// </summary>
    public class FrameSentArgs : System.EventArgs
    {
        public Frame Frame { get; private set; }

        public FrameSentArgs(Frame frame)
        {
            Frame = frame;
        }
    }
}
