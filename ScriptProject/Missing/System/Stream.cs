﻿namespace System
{
    using System.Runtime.CompilerServices;

    using Missing;

    [Imported]
    [ModuleName(null)]
    [IgnoreNamespace]
    public class Stream
    {
        public Stream(ArrayBuffer arrayBuffer)
        {
            throw new ImplementedInJavaScript();
        }

        public char ReadByte()
        {
            throw new ImplementedInJavaScript();
        }

        public void Seek(int position, object begin)
        {
            throw new ImplementedInJavaScript();
        }

        public void Close()
        {
            throw new ImplementedInJavaScript();
        }

        public int Position
        {
            get
            {
                throw new ImplementedInJavaScript();
            }
        }

        public int Length
        {
            get
            {
                throw new ImplementedInJavaScript();
            }
        }

        public int Read(Uint8Array buffer, int offset, int count)
        {
            throw new ImplementedInJavaScript();
        }

        public int ReadInt32()
        {
            throw new ImplementedInJavaScript();
        }

        public float ReadFloat32()
        {
            throw new ImplementedInJavaScript();
        }

        public double ReadFloat64()
        {
            throw new ImplementedInJavaScript();
        }

        public Uint8Array ReadBytes(int size)
        {
            throw new ImplementedInJavaScript();
        }

        public ArrayBuffer Buffer { get; set; }

        public ArrayBuffer BufferSubArray { get; private set; }

        internal void WriteInt32(int value)
        {
            throw new ImplementedInJavaScript();
        }

        internal void WriteFloat32(float value)
        {
            throw new ImplementedInJavaScript();
        }

        internal void WriteUint8Array(Uint8Array array)
        {
            throw new ImplementedInJavaScript();
        }
    }
}