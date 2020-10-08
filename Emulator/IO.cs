// IO.cs
// Copyright © 2020 Kenneth Gober
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.


using System;
using System.Net.Sockets;
using System.Threading;

namespace Emulator
{
    abstract class IO
    {
        public abstract Boolean CommandReady { get; }
        public abstract Boolean ReadReady { get; }
        public abstract Boolean WriteReady { get; }
        public abstract Boolean Test(Int16 word);
        public abstract void Command(Int16 word);
        public abstract Int16 Read();
        public abstract void Write(Int16 word);
    }

    class Teletype : IO
    {
        private Thread mWorker;
        private NetworkStream mNetwork;

        public Teletype()
        {
            mWorker = new Thread(new ThreadStart(WorkerThread));
            mWorker.Start();
        }

        public override Boolean CommandReady
        {
            get { return true; }
        }

        public override Boolean ReadReady
        {
            get
            {
                if (mNetwork == null) return false;
                return mNetwork.DataAvailable;
            }
        }

        public override Boolean WriteReady
        {
            get
            {
                if (mNetwork == null) return false;
                return true;
            }
        }

        public override Boolean Test(Int16 word)
        {
            return true;
        }

        public override void Command(Int16 word)
        {
        }

        public override Int16 Read()
        {
            return (Int16)(mNetwork.ReadByte());
        }

        public override void Write(Int16 word)
        {
            mNetwork.WriteByte((Byte)((word >> 8) & 0xff));
        }

        private void WorkerThread()
        {
            TcpListener L = new TcpListener(8101);
            L.Start();
            while (true)
            {
                TcpClient C = L.AcceptTcpClient();
                mNetwork = C.GetStream();
                while (C.Connected) Thread.Sleep(100);
                C.Close();
                mNetwork = null;
            }
        }
    }
}
