﻿using System;
using System.Diagnostics.Contracts;

namespace SharedProtocol.Framing
{
    /// <summary>
    /// Frame headers class
    /// </summary>
    public class HeadersFrame : Frame, IEndStreamFrame
    {
        // The number of bytes in the frame, not including the compressed headers.
        private const int PreambleSizeWithPriority = 12;

        // The number of bytes in the frame, not including the compressed headers.
        private const int PreambleSizeWithoutPriority = 8;

        private HeadersList _headers = new HeadersList();

        // 8 bits, 24-31
        public bool IsEndStream
        {
            get
            {
                return (Flags & FrameFlags.EndStream) == FrameFlags.EndStream;
            }
            set
            {
                if (value)
                {
                    Flags |= FrameFlags.EndStream;
                }
            }
        }

        public bool HasPriority
        {
            get { return (Flags & FrameFlags.Priority) == FrameFlags.Priority; }
            private set
            {
                if (value)
                {
                    Flags |= FrameFlags.Priority;
                }
            }
        }

        public bool IsEndHeaders
        {
            get
            {
                return (Flags & FrameFlags.EndHeaders) == FrameFlags.EndHeaders;
            }
            set
            {
                if (value)
                {
                    Flags |= FrameFlags.EndHeaders;
                }
            }
        }

        public Priority Priority
        {
            get
            {
                if (!HasPriority)
                    return Priority.Pri7;

                return (Priority)FrameHelpers.Get32BitsAt(Buffer, 8);
            }
            private set
            {
                FrameHelpers.Set32BitsAt(Buffer, 8, (int)value);
            }
        }

        public ArraySegment<byte> CompressedHeaders
        {
            get
            {
                int offset = HasPriority ? PreambleSizeWithPriority : PreambleSizeWithoutPriority;
                return new ArraySegment<byte>(Buffer, offset, Buffer.Length - offset);
            }
        }

        public HeadersList Headers
        {
            get
            {
                return _headers;
            }
        }

        /// <summary>
        ///  Create an outgoing frame
        /// </summary>
        /// <param name="streamId">Stream id</param>
        /// <param name="headerBytes">Header bytes</param>
        /// <param name="priority">Priority</param>
        public HeadersFrame(int streamId, byte[] headerBytes, Priority priority = Priority.Pri7)
        {
            //PRIORITY (0x8):  Bit 4 being set indicates that the first four octets
            //of this frame contain a single reserved bit and a 31-bit priority;
            //If this bit is not set, the four bytes do not
            //appear and the frame only contains a header block fragment.
            bool hasPriority = (priority != Priority.None);

            int preambleLength = hasPriority
                ? PreambleSizeWithPriority
                : PreambleSizeWithoutPriority;

            _buffer = new byte[headerBytes.Length + preambleLength];
            HasPriority = hasPriority;

            StreamId = streamId;
            FrameType = FrameType.Headers;
            FrameLength = Buffer.Length - Constants.FramePreambleSize;
            if (HasPriority)
            {
                Priority = priority;
            }

            // Copy in the headers
            System.Buffer.BlockCopy(headerBytes, 0, Buffer, preambleLength, headerBytes.Length);
        }

        /// <summary>
        /// Create an incoming frame
        /// </summary>
        /// <param name="preamble">Frame preamble</param>
        public HeadersFrame(Frame preamble)
            : base(preamble)
        {
        }

        /// <summary>
        /// Creates headers frame
        /// </summary>
        public HeadersFrame(int streamId, bool fin)
        {
            _buffer = new byte[PreambleSizeWithPriority];
            StreamId = streamId;
            IsEndHeaders = true;
            IsEndStream = fin;
        }
    }
}
