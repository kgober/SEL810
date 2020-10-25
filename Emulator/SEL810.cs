// SEL810.cs
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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Emulator
{
    class SEL810
    {
        public const Int32 CORE_SIZE = 32768;       // number of words of memory
        public const Int32 DEFAULT_GUI_PORT = 8100; // TCP port for front panel
        public const Int32 DEFAULT_TTY_PORT = 8101; // TCP port for console TTY

        private static TimeSpan sIndicatorLag = new TimeSpan(0, 0, 0, 0, 200);

        private volatile Boolean vExitGUI = false;
        private volatile Boolean vStep = false;
        private volatile Boolean vHalt = true;
        private volatile Boolean vIOHold = false;
        private volatile Boolean vInterrupt = false;
        private volatile Boolean vOverflow = false;
        private volatile Boolean vBPR = false;
        private volatile Boolean vBPW = false;
        private volatile Boolean vBPA = false;
        private volatile Boolean vBPB = false;
        private volatile Boolean vBPIR = false;
        private volatile Boolean vBPPC = false;

        private Object mLock = new Object();
        private Thread mCPUThread;
        private Thread mGUIThread;
        private Socket mGUISocket;
        private Int32 mGUIProtocol;
        private Int32 mGUIProtocolState;
        private JSON.Value mGUIState;
        private volatile Boolean vGUIDirty;

        private UInt16[] mCore = new UInt16[CORE_SIZE];
        private UInt16 mT, mA, mB, mPC, mIR, mSR, mX, mPPR, mVBR;
        private Boolean mCF, mXP;

        private UInt16[] mIntRequest = new UInt16[9]; // interrupt request
        private UInt16[] mIntEnabled = new UInt16[9]; // interrupt enabled
        private UInt16[] mIntActive = new UInt16[9]; // interrupt active
        private Boolean mTOI, mIntBlocked;
        private Int32 mIntGroup = 8;
        private Int32 mIntLevel = 1;
        private UInt16 mIntMask = 0;

        private Int16[] mBPR = new Int16[CORE_SIZE];
        private Int16[] mBPW = new Int16[CORE_SIZE];
        private Boolean[] mBPA = new Boolean[65536];
        private Boolean[] mBPB = new Boolean[65536];
        private Boolean[] mBPIR = new Boolean[65536];
        private Boolean[] mBPPC = new Boolean[32768];

        private IO[] mIO = new IO[64];

        public SEL810()
        {
            mGUIThread = new Thread(new ThreadStart(GUIThread));
            mGUIThread.Start();

            mCPUThread = new Thread(new ThreadStart(CPUThread));
            mCPUThread.Start();

            mIO[1] = new Teletype(DEFAULT_TTY_PORT);
        }

        public Boolean IsHalted
        {
            get { return vHalt; }
        }

        public UInt16 A
        {
            get { return mA; } // TODO: make thread-safe
            set // TODO: make thread-safe
            {
                if (vBPA)
                {
                    lock (mBPA)
                    {
                        if (mBPA[value])
                        {
                            Halt();
                            Console.Out.Write("[A:{0:x4}/{1}]", value, Program.Octal(value, 6));
                        }
                    }
                }
                if (mGUIProtocol == 1)
                {
                    mGUIState["A Register"] = new JSON.Value(value);
                    vGUIDirty = true;
                }
                mA = value;
            }
        }

        public UInt16 B
        {
            get { return mB; } // TODO: make thread-safe
            set // TODO: make thread-safe
            {
                if (vBPB)
                {
                    lock (mBPB)
                    {
                        if (mBPB[value])
                        {
                            Halt();
                            Console.Out.Write("[B:{0:x4}/{1}]", value, Program.Octal(value, 6));
                        }
                    }
                }
                if (mGUIProtocol == 1)
                {
                    mGUIState["B Register"] = new JSON.Value(value);
                    vGUIDirty = true;
                }
                mB = value;
            }
        }

        public UInt16 T
        {
            get { return mT; } // TODO: make thread-safe
            set // TOOD: make thread-safe
            {
                if (mGUIProtocol == 1)
                {
                    mGUIState["Transfer Register"] = new JSON.Value(value);
                    vGUIDirty = true;
                }
                mT = value;
            }
        }

        public UInt16 PC
        {
            get { return mPC; } // TODO: make thread-safe
            set // TODO: make thread-safe
            {
                value &= 0x7fff;
                if (vBPPC)
                {
                    lock (mBPPC)
                    {
                        if (mBPPC[value])
                        {
                            Halt();
                            Console.Out.Write("[PC:{0:x4}/{1}]", value, Program.Octal(value, 5));
                        }
                    }
                }
                if (mGUIProtocol == 1)
                {
                    mGUIState["Program Counter"] = new JSON.Value(value);
                    vGUIDirty = true;
                }
                mPC = value;
            }
        }

        public UInt16 IR
        {
            get { return mIR; } // TODO: make thread-safe
            set // TODO: make thread-safe
            {
                if (vBPIR)
                {
                    lock (mBPIR)
                    {
                        if (mBPIR[value])
                        {
                            Halt();
                            Console.Out.Write("[IR:{0:x4}/{1}]", value, Program.Octal(value, 6));
                        }
                    }
                }
                if (mGUIProtocol == 1)
                {
                    mGUIState["Instruction"] = new JSON.Value(value);
                    vGUIDirty = true;
                }
                mIR = value;
            }
        }

        public UInt16 SR
        {
            get { return mSR; } // TODO: make thread-safe
            set { mSR = value; } // TODO: make thread-safe
        }

        public Teletype.Mode ConsoleMode
        {
            get { return (mIO[1] as Teletype).OutputMode; } // TODO: make thread-safe
            set { (mIO[1] as Teletype).OutputMode = value; } // TODO: make thread-safe
        }

        public UInt16 this[Int32 index]
        {
            get { return mCore[index]; } // TODO: make thread-safe
            set { mCore[index] = value; } // TODO: make thread-safe
        }

        public void MasterClear()
        {
            T = B = A = IR = PC = 0;
            mVBR = 0;
            ClearOverflow();
            mCF = false;
        }

        public void Load(Int32 loadAddress, String imageFile)
        {
            Load(loadAddress, File.ReadAllBytes(imageFile));
        }

        public void Load(Int32 loadAddress, Byte[] bytesToLoad)
        {
            Load(loadAddress, bytesToLoad, 0, bytesToLoad.Length);
        }

        public void Load(Int32 loadAddress, Byte[] bytesToLoad, Int32 offset, Int32 count)
        {
            while (count-- > 0)
            {
                Int32 word = bytesToLoad[offset++] << 8;
                if (count-- > 0) word |= bytesToLoad[offset++];
                loadAddress %= CORE_SIZE;
                mCore[loadAddress++] = (UInt16)(word);
            }
        }

        public void SetGUIProtocol(Int32 protocol)
        {
            mGUIProtocol = protocol;
            if (protocol == 1) mGUIState = JSON.Value.ReadFrom(@"{
                ""Program Counter"": 0,
                ""A Register"": 0,
                ""B Register"": 0,
                ""Control Switches"": 0,
                ""Instruction"": 0,
                ""Interrupt Register"": 0,
                ""Transfer Register"": 0,
                ""Protect Register"": 0,
                ""VBR Register"": 0,
                ""Index Register"": 0,
                ""Stall Counter"": 0,
                ""halt"": true,
                ""iowait"": false,
                ""overflow"": false,
                ""master_clear"": false,
                ""parity"": false,
                ""display"": false,
                ""enter"": false,
                ""step"": false,
                ""io_hold_release"": false,
                ""cold_boot"": false,
                ""carry"": false,
                ""protect"": false,
                ""mode_key"": false,
                ""index_pointer"": false,
                ""stall"": false,
                ""assembler"": ""RNA"",
                ""sim_ticks"": 0,
                ""Program Counter_pwm"": [0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0],
                ""A Register_pwm"": [0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0],
                ""B Register_pwm"": [0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0],
                ""Instruction_pwm"": [0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0],
                ""Transfer Register_pwm"": [0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]
            }");
        }

        public void SetReader(String inputFile)
        {
            Teletype cons = mIO[1] as Teletype;
            cons.SetReader(inputFile);
        }

        public void SetPunch(String outputFile)
        {
            Teletype cons = mIO[1] as Teletype;
            cons.SetPunch(outputFile);
        }

        public void TTY_KeyIn(Char ch)
        {
            Teletype cons = mIO[1] as Teletype;
            cons.KeyIn(ch);
        }

        public void TTY_PrtOut(Char ch)
        {
            Teletype cons = mIO[1] as Teletype;
            cons.PrtOut(ch);
        }

        public void AttachDevice(Int32 unit, String destination)
        {
            IO device;
            lock (mIO)
            {
                device = mIO[unit];
                mIO[unit] = null;
            }
            if (device != null) device.Exit();
            if ((destination == null) || (destination.Length == 0)) return;
            Int32 port;
            Int32 p = destination.IndexOf(':');
            if (p == -1)
            {
                port = 8100 + unit;
            }
            else if (!Int32.TryParse(destination.Substring(p + 1), out port))
            {
                Console.Out.WriteLine("Unrecognized TCP port: {0}", destination.Substring(p + 1));
                return;
            }
            else if ((port < 1) || (port > 65535))
            {
                Console.Out.WriteLine("Invalid TCP port: {0}", destination.Substring(p + 1));
                return;
            }
            device = new NetworkDevice(destination, port);
            lock (mIO) mIO[unit] = device;
        }

        public void Run()
        {
            if (vHalt)
            {
                Console.Out.Write("[RUN]");
                if (mGUIProtocol == 1)
                {
                    mGUIState["halt"] = new JSON.Value(false);
                    vGUIDirty = true;
                }
            }
            vHalt = false;
        }

        public void Halt()
        {
            if (!vHalt)
            {
                Console.Out.Write("[HALT]");
                if (mGUIProtocol == 1)
                {
                    mGUIState["halt"] = new JSON.Value(true);
                    vGUIDirty = true;
                }
            }
            vHalt = true;
        }

        public void Step()
        {
            vStep = true;
            while (vStep) Thread.Sleep(50);
        }

        private void SetIOHold()
        {
            if (!vIOHold)
            {
                if (Program.VERBOSE) Console.Out.Write("[+IOH]");
                if (mGUIProtocol == 1)
                {
                    mGUIState["iowait"] = new JSON.Value(true);
                    vGUIDirty = true;
                }
            }
            vIOHold = true;
        }

        public void ReleaseIOHold()
        {
            if (vIOHold)
            {
                if (Program.VERBOSE) Console.Out.Write("[-IOH]");
                if (mGUIProtocol == 1)
                {
                    mGUIState["iowait"] = new JSON.Value(false);
                    vGUIDirty = true;
                }
            }
            vIOHold = false;
        }

        private void SetInterrupt(Int32 group, Int32 level)
        {
            if (!vInterrupt)
            {
                if (Program.VERBOSE) Console.Out.Write("[+INT{0:D0}:{1:D0}]", group, level);
            }
            vInterrupt = true;
        }

        private void ClearInterrupt()
        {
            if (vInterrupt)
            {
                if (Program.VERBOSE) Console.Out.Write("[-INT]");
            }
            vInterrupt = false;
        }

        private void SetOverflow()
        {
            if (!vOverflow)
            {
                if (Program.VERBOSE) Console.Out.Write("[+OVF]");
                if (mGUIProtocol == 1)
                {
                    mGUIState["overflow"] = new JSON.Value(true);
                    vGUIDirty = true;
                }
            }
            vOverflow = true;
        }

        private void ClearOverflow()
        {
            if (vOverflow)
            {
                if (Program.VERBOSE) Console.Out.Write("[-OVF]");
                if (mGUIProtocol == 1)
                {
                    mGUIState["overflow"] = new JSON.Value(false);
                    vGUIDirty = true;
                }
            }
            vOverflow = false;
        }

        public void Exit()
        {
            lock (mIO)
            {
                for (Int32 i = 0; i < mIO.Length; i++)
                {
                    if (mIO[i] != null) mIO[i].Exit();
                    mIO[i] = null;
                }
            }
            vExitGUI = true;
            mGUIThread.Join();
            mCPUThread.Abort();
            mCPUThread.Join();
        }

        public Int16 GetBPR(UInt16 addr)
        {
            if (!vBPR) return 0;
            lock (mBPR) return mBPR[addr];
        }

        public void SetBPR(UInt16 addr, Int16 count)
        {
            lock (mBPR)
            {
                mBPR[addr] = count;
                if (count != 0)
                {
                    vBPR = true;
                    return;
                }
                for (Int32 i = 0; i < mBPR.Length; i++)
                {
                    if (mBPR[i] == 0) continue;
                    vBPR = true;
                    return;
                }
                vBPR = false;
            }
        }

        public Int16 GetBPW(UInt16 addr)
        {
            if (!vBPR) return 0;
            lock (mBPW) return mBPW[addr];
        }

        public void SetBPW(UInt16 addr, Int16 count)
        {
            lock (mBPW)
            {
                mBPW[addr] = count;
                if (count != 0)
                {
                    vBPW = true;
                    return;
                }
                for (Int32 i = 0; i < mBPW.Length; i++)
                {
                    if (mBPW[i] == 0) continue;
                    vBPW = true;
                    return;
                }
                vBPW = false;
            }
        }

        public Boolean GetBPReg(Int32 index, UInt16 value)
        {
            switch (index)
            {
                case 0: return mBPA[value];
                case 1: return mBPB[value];
                case 2: return mBPIR[value];
                case 3: return mBPPC[value];
            }
            return false;
        }

        public void SetBPReg(Int32 index, UInt16 value)
        {
            switch (index)
            {
                case 0: mBPA[value] = vBPA = true; return;
                case 1: mBPB[value] = vBPB = true; return;
                case 2: mBPIR[value] = vBPIR = true; return;
                case 3: mBPPC[value] = vBPPC = true; return;
            }
        }

        public void ClearBPReg(Int32 index, UInt16 value)
        {
            switch (index)
            {
                case 0: mBPA[value] = false; return; // TODO: set vBPA false if no breakpoints remain
                case 1: mBPB[value] = false; return; // TODO: set vBPB false if no breakpoints remain
                case 2: mBPIR[value] = false; return; // TODO: set vBPIR false if no breakpoints remain
                case 3: mBPPC[value] = false; return; // TODO: set vBPPC false if no breakpoints remain
            }
        }

        private void RefreshGUI()
        {
            Socket gui = mGUISocket;
            if (gui == null) return;
            if (mGUIProtocol == 1)
            {
                String s = mGUIState.ToString();
                Byte[] buf = Encoding.ASCII.GetBytes(s);
                Byte[] len = new Byte[2];
                Int32 n = buf.Length;
                len[0] = (Byte)((n >> 8) & 255);
                len[1] = (Byte)(n & 255);
                gui.Send(len, 0, 2, SocketFlags.None); // TODO: verify return code
                gui.Send(buf, 0, n, SocketFlags.None); // TODO: verify return code
            }
            vGUIDirty = false;
        }

        private void GUIThread()
        {
            TcpListener L = new TcpListener(IPAddress.Any, DEFAULT_GUI_PORT);
            L.Start();
            while (!vExitGUI)
            {
                while ((!L.Pending()) && (!vExitGUI)) Thread.Sleep(100);
                if (vExitGUI) break;
                TcpClient C = L.AcceptTcpClient();
                mGUIProtocolState = 0;
                lock (this) mGUISocket = C.Client;
                Console.Out.Write("[+GUI]");
                RefreshGUI();
                while ((C.Connected) && (!vExitGUI))
                {
                    Thread.Sleep(200);
                    StepFrontPanel();
                }
                C.Close();
                Console.Out.Write("[-GUI]");
                lock (this) mGUISocket = null;
            }
            L.Stop();
        }

        private void CPUThread()
        {
            while (true)
            {
                while ((vHalt) && (!vStep))
                {
                    Thread.Sleep(100);
                }
                if (vStep)
                {
                    StepCPU();
                    StepInterrupts();
                    vStep = false;
                }
                while (!vHalt)
                {
                    StepCPU();
                    StepInterrupts();
                }
            }
        }

        private void StepCPU()
        {
            // o ooo xim aaa aaa aaa - memory reference instruction
            // o ooo xis sss aaa aaa - augmented instruction
            UInt16 r16;
            Int16 s16;
            Int32 s32, ea;
            Boolean i, m;
            UInt16 PC_inc = 1;
            Int32 op = (IR >> 12) & 15;
            if (op == 0) // augmented 00 instructions
            {
                Int32 aug = IR & 63;
                Int32 sc = (IR >> 6) & 15;
                switch (aug)
                {
                    case 0: // HLT - halt
                        IR = Read(mPC);
                        Halt();
                        return;
                    case 1: // 00-01 RNA - round A
                        if ((B & 0x4000) != 0) A++;
                        if ((A & 0x8000) != 0) SetOverflow();
                        break;
                    case 2: // 00-02 NEG - negate A
                        if (A == 0x8000) SetOverflow();
                        A = (UInt16)(-A - ((mCF) ? 1 : 0));
                        break;
                    case 3: // CLA - clear A
                        A = 0;
                        break;
                    case 4: // TBA - transfer B to A
                        A = B;
                        break;
                    case 5: // TAB - transfer A to B
                        B = A;
                        break;
                    case 6: // 00-06 IAB - interchange A and B
                        T = A;
                        A = B;
                        B = T;
                        break;
                    case 7: // 00-07 CSB - copy sign of B
                        if ((B & 0x8000) != 0)
                        {
                            mCF = true; // TODO: find out exactly which instructions CF affects
                            B &= 0x7fff;
                        }
                        mIntBlocked = true;
                        break;
                    case 8: // RSA - right shift arithmetic
                        s16 = (Int16)(A);
                        A = (UInt16)(s16 >> sc);
                        break;
                    case 9: // LSA - left shift arithmetic
                        s32 = A << sc;
                        A = (UInt16)((A & 0x8000) | (s32 & 0x7fff));
                        break;
                    case 10: // FRA - full right arithmetic shift
                        s32 = (A << 16) | ((B & 0x7fff) << 1);
                        s32 >>= sc;
                        A = (UInt16)(s32 >> 16);
                        B = (UInt16)((B & 0x8000) | ((s32 >> 1) & 0x7fff));
                        break;
                    case 11: // FLL - full left logical shift
                        s32 = (A << 16) | B;
                        s32 <<= sc;
                        A = (UInt16)(s32 >> 16);
                        B = (UInt16)(s32);
                        break;
                    case 12: // FRL - full rotate left
                        Int64 r64 = (A << 16) | B;
                        r64 <<= sc;
                        A = (UInt16)(r64 >> 16);
                        B = (UInt16)((r64 & 0xffff) | ((r64 >> 32) & ((1 << sc) - 1)));
                        break;
                    case 13: // RSL - right shift logical
                        A >>= sc;
                        break;
                    case 14: // LSL - logical shift left
                        A <<= sc;
                        break;
                    case 15: // FLA - full left arithmetic shift
                        s32 = (A << 16) | ((B & 0x7fff) << 1);
                        s32 <<= sc;
                        A = (UInt16)((A & 0x8000) | ((s32 >> 16) & 0x7fff));
                        B = (UInt16)((B & 0x8000) | ((s32 >> 1) & 0x7fff));
                        break;
                    case 16: // ASC - complement sign of accumulator
                        A ^= 0x8000;
                        break;
                    case 17: // 00-21 SAS - skip on accumulator sign
                        s16 = (Int16)(A);
                        if (s16 > 0) ++mPC;
                        if (s16 >= 0) ++mPC;
                        break;
                    case 18: // SAZ - skip if accumulator zero
                        if (A == 0) ++mPC;
                        break;
                    case 19: // SAN - skip if accumulator negative
                        if ((A & 0x8000) != 0) ++mPC;
                        break;
                    case 20: // SAP - skip if accumulator positive
                        if ((A & 0x8000) == 0) ++mPC;
                        break;
                    case 21: // 00-25 SOF - skip if no overflow
                        if (vOverflow) ClearOverflow();
                        else ++mPC;
                        break;
                    case 22: // IBS - increment B and skip
                        if ((++B & 0x8000) == 0) ++mPC;
                        break;
                    case 23: // 00-27 ABA - and B and A accumulators
                        A &= B;
                        break;
                    case 24: // 00-30 OBA - or B and A accumulators
                        A |= B;
                        break;
                    case 25: // 00-31 LCS - load control switches
                        T = SR;
                        A = T;
                        break;
                    case 26: // 00-32 SNO - skip normalized accumulator
                        if ((A & 0x8000) != ((A << 1) & 0x8000)) ++mPC;
                        break;
                    case 27: // NOP - no operation
                        break;
                    case 28: // 00-34 CNS - convert number system
                        if (A == 0x8000) SetOverflow();
                        if ((A & 0x8000) != 0) A = (UInt16)(0x8000 | ((-A) & 0x7fff));
                        break;
                    case 29: // 00-35 TOI - turn off interrupt
                        mIntBlocked = true;
                        mTOI = true;
                        break;
                    case 30: // 00-36 LOB - long branch
                        T = Read(++PC);
                        PC = (UInt16)(T & 0x7fff);
                        PC_inc = 0;
                        if (mTOI) DoTOI();
                        break;
                    case 31: // 00-37 OVS - set overflow
                        SetOverflow();
                        break;
                    case 32: // TBP - transfer B to protect register
                        mPPR = B;
                        break;
                    case 33: // TPB - transfer protect register to B
                        B = mPPR;
                        break;
                    case 34: // TBV - transfer B to variable base register
                        mVBR = (UInt16)(B & 0x7e00);
                        break;
                    case 35: // TVB - transfer variable base register to B
                        B = mVBR;
                        break;
                    case 36: // STX - store index
                        i = ((IR & 0x400) != 0); // I flag
                        m = ((IR & 0x200) != 0); // M flag
                        ++PC;
                        ea = (i) ? Indirect(PC, m) : PC;
                        Write(ea, mX);
                        break;
                    case 37: // LIX - load index
                        i = ((IR & 0x400) != 0); // I flag
                        m = ((IR & 0x200) != 0); // M flag
                        ++PC;
                        ea = (i) ? Indirect(PC, m) : PC;
                        T = Read(ea);
                        mX = T;
                        break;
                    case 38: // XPX - set index pointer to index register
                        mXP = true;
                        break;
                    case 39: // XPB - set index pointer to B
                        mXP = false;
                        break;
                    case 40: // 00-50 SXB - skip if index register is B
                        if (!mXP) ++mPC;
                        break;
                    case 41: // 00-N-51 IXS - increment index and skip if positive
                        mX += (UInt16)(sc);
                        if ((mX & 0x8000) == 0) ++mPC;
                        break;
                    case 42: // TAX - transfer A to index register
                        mX = A;
                        break;
                    case 43: // TXA - transfer index register to A
                        A = mX;
                        break;
                    case 44: // RTX
                    default: // TODO: what do undefined opcodes do?
                        break;
                }
            }
            else if (op == 11) // augmented 13 instructions
            {
                Int32 aug = (IR >> 6) & 7;
                Int32 unit = IR & 0x3f;
                i = ((IR & 0x400) != 0); // I flag
                m = ((IR & 0x200) != 0); // M flag
                switch (aug)
                {
                    case 0: // CEU - command external unit (skip mode)
                        ++PC;
                        ea = (i) ? Indirect(PC, m) : PC;
                        T = Read(ea);
                        if (IO_Command(unit, T, false)) ++mPC;
                        break;
                    case 1: // CEU - command external unit (wait mode)
                        ++PC;
                        ea = (i) ? Indirect(PC, m) : PC;
                        T = Read(ea);
                        IO_Command(unit, T, true);
                        break;
                    case 2: // TEU - test external unit
                        ++PC;
                        ea = (i) ? Indirect(PC, m) : PC;
                        T = Read(ea);
                        if (IO_Test(unit, T)) ++mPC;
                        break;
                    case 4: // 13-04-N SNS - sense numbered switch
                        unit &= 15;
                        T = SR;
                        if (((T << unit) & 0x8000) == 0) ++mPC;
                        break;
                    case 6:
                        if (unit == 0) // 130600 PIE - priority interrupt enable
                        {
                            T = Read(++PC);
                            Int32 grp = (T >> 12) & 7;
                            mIntEnabled[grp] |= (UInt16)(T & 0x0fff);
                            mIntBlocked = true;
                        }
                        else if (unit == 1) // PID - priority interrupt disable
                        {
                            T = Read(++PC);
                            Int32 grp = (T >> 12) & 7;
                            mIntEnabled[grp] &= (UInt16)(~(T & 0x0fff));
                            mIntBlocked = true;
                        }
                        break;
                }
            }
            else if (op == 15) // augmented 17 instructions
            {
                Int32 aug = (IR >> 6) & 7;
                Int32 unit = IR & 0x3f;
                Boolean r = ((IR & 0x800) != 0); // R flag
                i = ((IR & 0x400) != 0); // I flag
                m = ((IR & 0x200) != 0); // M flag
                switch (aug)
                {
                    case 0: // AOP - accumulator output to peripheral (skip mode)
                        if (IO_Write(unit, A, false)) ++mPC;
                        break;
                    case 1: // AOP - accumulator output to peripheral (wait mode)
                        IO_Write(unit, A, true);
                        break;
                    case 2: // AIP - accumulator input from peripheral (skip mode)
                        if (IO_Read(unit, out r16, false)) // TODO: does this go through T?
                        {
                            if (r) r16 |= A;
                            A = r16;
                            ++mPC;
                        }
                        break;
                    case 3: // AIP - accumulator input from peripheral (wait mode)
                        IO_Read(unit, out r16, true); // TODO: does this go through T?
                        if (r) r16 |= A;
                        A = r16;
                        break;
                    case 4: // MOP - memory output to peripheral (skip mode)
                        ++PC;
                        ea = (i) ? Indirect(PC, m) : PC;
                        T = Read(ea);
                        if (IO_Write(unit, T, false)) ++mPC;
                        break;
                    case 5: // MOP - memory output to peripheral (wait mode)
                        ++PC;
                        ea = (i) ? Indirect(PC, m) : PC;
                        T = Read(ea);
                        IO_Write(unit, T, true);
                        break;
                    case 6: // MIP - memory input from peripheral (skip mode)
                        ++PC;
                        ea = (i) ? Indirect(PC, m) : PC;
                        if (IO_Read(unit, out r16, false)) ++mPC; // TODO: does this go through T?
                        Write(ea, r16);
                        break;
                    case 7: // MIP - memory input from peripheral (wait mode)
                        ++PC;
                        ea = (i) ? Indirect(PC, m) : PC;
                        IO_Read(unit, out r16, true); // TODO: does this go through T?
                        Write(ea, r16);
                        break;
                }
            }
            else
            {
                Boolean x = ((IR & 0x800) != 0); // X flag
                i = ((IR & 0x400) != 0); // I flag
                m = ((IR & 0x200) != 0); // M flag
                ea = IR & 511; // TODO: should be T & 511, verify that this works
                if (m) ea |= PC & 0x7e00;
                if (x) ea += (Int16)((mXP) ? mX : B);
                if (!m && !x) ea |= mVBR;
                while (i)
                {
                    T = Read(ea);
                    x = ((T & 0x8000) != 0);
                    i = ((T & 0x4000) != 0);
                    ea = (PC & 0x4000) | (T & 0x3fff);
                    if (x) ea += (Int16)((mXP) ? mX : B);
                }
                switch (op)
                {
                    case 1: // 01 LAA - load A accumulator
                        T = Read(ea);
                        A = T;
                        break;
                    case 2: // LBA - load B accumulator
                        T = Read(ea);
                        B = T;
                        break;
                    case 3: // STA - store A accumulator
                        Write(ea, A);
                        break;
                    case 4: // STB - store B accumulator
                        Write(ea, B);
                        break;
                    case 5: // 05 AMA - add memory to A
                        T = Read(ea);
                        r16 = (UInt16)(A + T + ((mCF) ? 1 : 0));
                        if (((A & 0x8000) == (T & 0x8000)) && ((A & 0x8000) != (r16 & 0x8000))) SetOverflow();
                        A = r16;
                        break;
                    case 6: // 06 SMA - subtract memory from A
                        T = Read(ea);
                        r16 = (UInt16)(A - T - ((mCF) ? 1 : 0));
                        if (((A & 0x8000) != (T & 0x8000)) && ((A & 0x8000) != (r16 & 0x8000))) SetOverflow();
                        A = r16;
                        break;
                    case 7: // 07 MPY - multiply
                        T = Read(ea);
                        s32 = (Int16)(T) * (Int16)(B);
                        if ((T == 0x8000) && (B == 0x8000)) SetOverflow();
                        B = (UInt16)(s32 & 0x7fff);
                        A = (UInt16)(s32 >> 15);
                        break;
                    case 8: // 10 DIV - divide
                        T = Read(ea);
                        s16 = (Int16)T;
                        s32 = ((A == 0) && ((B & 0x8000) != 0)) ? 0xffff << 16 : A << 16;
                        s32 = (s32 >> 1) | (B & 0x7fff);
                        B = (UInt16)(s32 % s16);
                        A = (UInt16)(s32 = s32 / s16);
                        if ((s32 < -32767) || (s32 > 32767)) SetOverflow();
                        break;
                    case 9: // 11 BRU - branch unconditional
                        PC = (UInt16)(ea);
                        PC_inc = 0;
                        if ((mTOI) && ((IR & 0x400) != 0)) DoTOI();
                        break;
                    case 10: // 12 SPB - store place and branch
                        Write(ea, (UInt16)(++PC & 0x3fff)); // save only 14 bits, BRU* will see high bits as X=0 I=0
                        PC = (UInt16)(ea);
                        mIntBlocked = true;
                        break;
                    case 12: // 14 IMS - increment memory and skip
                        r16 = T = Read(ea);
                        Write(ea, ++r16);
                        if (r16 == 0) ++mPC;
                        break;
                    case 13: // 15 CMA - compare memory and accumulator
                        T = Read(ea);
                        s16 = (Int16)(A - T);
                        if (s16 > 0) ++mPC;
                        if (s16 >= 0) ++mPC;
                        break;
                    case 14: // 16 AMB - add memory to B
                        T = Read(ea);
                        r16 = (UInt16)(B + T + ((mCF) ? 1 : 0));
                        if (((B & 0x8000) == (T & 0x8000)) && ((B & 0x8000) != (r16 & 0x8000))) SetOverflow();
                        B = r16;
                        break;
                }
            }
            if (IR != 7) mCF = false;
            if (PC_inc != 0) PC += PC_inc;
            T = Read(PC);
            IR = T;
        }

        private UInt16 Indirect(UInt16 addr, Boolean M)
        {
            Boolean x, i;
            Int32 ea = addr;
            do
            {
                T = Read(ea);
                x = ((T & 0x8000) != 0);
                i = ((T & 0x4000) != 0);
                ea = T & 0x3fff;
                if (M) ea |= PC & 0x4000;
                if (x) ea += (Int16)((mXP) ? mX : B);
            }
            while (i);
            return (UInt16)(ea);
        }

        private void DoTOI()
        {
            // clear active interrupt
            UInt16 mask = (UInt16)(~mIntMask);
            mIntActive[mIntGroup] &= mask;
            mIntRequest[mIntGroup] &= mask;
            mTOI = false;

            // check for another interrupt to become active
            for (Int32 i = 0; i < 8; i++)
            {
                if (mIntActive[i] == 0) continue;
                UInt16 A = mIntActive[i];
                mask = 1;
                Int32 lev = 1;
                while (mask != 0x1000)
                {
                    if ((A & mask) != 0) break;
                    mask <<= 1;
                    lev++;
                }
                mIntGroup = i;
                mIntLevel = lev;
                mIntMask = mask;
                return;
            }
            mIntGroup = 8;
            mIntLevel = 1;
            mIntMask = 0;
            ClearInterrupt();
        }

        private void StepInterrupts()
        {
            // check for interrupt requests
            for (Int32 unit = 0; unit < mIO.Length; unit++)
            {
                if (mIO[unit] == null) continue;
                UInt16[] IRQ = mIO[unit].Interrupts;
                if (IRQ == null) continue;
                for (Int32 grp = 0; grp < 8; grp++) if (IRQ[grp] != 0) mIntRequest[grp] |= IRQ[grp];
            }

            // check whether to trigger an interrupt
            if (mIntBlocked)
            {
                mIntBlocked = false;
            }
            else
            {
                for (Int32 grp = 0; grp <= mIntGroup; grp++)
                {
                    UInt16 mask = (UInt16)(mIntRequest[grp] & mIntEnabled[grp]);
                    if (mask == 0) continue;
                    if ((grp < mIntGroup) || ((mask & ((1 << (mIntLevel - 1)) - 1)) != 0)) 
                    {
                        // set new active interrupt group/level
                        mIntGroup = grp;
                        mIntMask = 1;
                        mIntLevel = 1;
                        while (mIntMask != 0x1000)
                        {
                            if ((mask & mIntMask) != 0) break;
                            mIntMask <<= 1;
                            mIntLevel++;
                        }
                        mIntActive[mIntGroup] |= mIntMask;

                        // select interrupt vector
                        Int32 ea = 514 + mIntGroup * 16 + mIntLevel - 1;
                        if (mIntGroup > 2) ea += 16; // skip '1060 range used by BTC
                        SetInterrupt(mIntGroup, mIntLevel);

                        // execute SPB* instruction
                        T = Read(ea);
                        ea = T & 0x7fff;
                        Write(ea, PC);
                        PC = (UInt16)(ea + 1);
                        IR = Read(PC);
                        mIntBlocked = true;
                        break;
                    }
                }
            }
        }

        private void StepFrontPanel()
        {
            Socket gui = mGUISocket;
            if (gui == null) return;
            if (vGUIDirty) RefreshGUI();
            Int32 n;
            try
            {
                n = gui.Available;
            }
            catch
            {
                return;
            }
            if (mGUIProtocol == 1)
            {
                if (n == 0) return;
                switch (mGUIProtocolState)
                {
                    case 0:
                        if (n < 2) return;
                        Byte[] len = new Byte[2];
                        gui.Receive(len, 0, 2, SocketFlags.None); // TODO: verify return code
                        mGUIProtocolState = len[0] * 256 + len[1];
                        break;
                    default:
                        if (n < mGUIProtocolState) return;
                        Byte[] buf = new Byte[mGUIProtocolState];
                        gui.Receive(buf, 0, mGUIProtocolState, SocketFlags.None); // TODO: verify return code
                        String s = Encoding.ASCII.GetString(buf);
                        Console.Out.WriteLine(s);
                        JSON.Value v = JSON.Value.ReadFrom(s);
                        mGUIProtocolState = 0;
                        break;
                }
            }
            else if (n != 0)
            {
                Byte[] buf = new Byte[n];
                gui.Receive(buf, 0, n, SocketFlags.None);
                // discard
            }
        }

        private UInt16 Read(Int32 addr)
        {
            if (vBPR)
            {
                Int16 n = mBPR[addr];
                if (n != 0)
                {
                    lock (mBPR)
                    {
                        n = mBPR[addr];
                        if (n > 0) mBPR[addr]--;
                    }
                    if ((n == 1) || (n == -1))
                    {
                        Halt();
                        Console.Out.Write("[PC:{0} IR:{1} {2}]", Program.Octal(PC, 5), Program.Octal(IR, 6), Program.Decode(PC, IR));
                    }
                }
            }
            return mCore[addr];
        }

        private UInt16 Write(Int32 addr, UInt16 value)
        {
            if (vBPW)
            {
                Int16 n = mBPW[addr];
                if (n != 0)
                {
                    lock (mBPW)
                    {
                        n = mBPW[addr];
                        if (n > 0) mBPW[addr]--;
                    }
                    if ((n == 1) || (n == -1))
                    {
                        Halt();
                        Console.Out.Write("[PC:{0} IR:{1} {2}]", Program.Octal(PC, 5), Program.Octal(IR, 6), Program.Decode(PC, IR));
                    }
                }
            }
            return mCore[addr] = value;
        }

        private Boolean IO_Test(Int32 unit, UInt16 command)
        {
            IO device = mIO[unit];
            if (device == null) return false;
            return device.Test(command);
        }

        private Boolean IO_Command(Int32 unit, UInt16 command, Boolean wait)
        {
            IO device = mIO[unit];
            if (device == null) return false; // TODO: what if wait=true?
            Boolean ready = device.CommandReady;
            if ((!wait) && (!ready)) return false;
            DateTime start = DateTime.Now;
            while (!ready)
            {
                Thread.Sleep(10);
                ready = device.CommandReady;
                if ((DateTime.Now - start) > sIndicatorLag) break;
            }
            if (!ready)
            {
                SetIOHold();
                do Thread.Sleep(50); while (vIOHold && !device.CommandReady);
                ReleaseIOHold();
            }
            return device.Command(command);
        }

        private Boolean IO_Read(Int32 unit, out UInt16 word, Boolean wait)
        {
            word = 0;
            IO device = mIO[unit];
            if (device == null) return false; // TODO: what if wait=true?
            Boolean ready = device.ReadReady;
            if ((!wait) && (!ready)) return false;
            DateTime start = DateTime.Now;
            while (!ready)
            {
                Thread.Sleep(10);
                ready = device.ReadReady;
                if ((DateTime.Now - start) > sIndicatorLag) break;
            }
            if (!ready)
            {
                SetIOHold();
                do Thread.Sleep(20); while (vIOHold && !device.ReadReady);
                ReleaseIOHold();
            }
            return device.Read(out word);
        }

        private Boolean IO_Write(Int32 unit, UInt16 word, Boolean wait)
        {
            IO device = mIO[unit];
            if (device == null) return false; // TODO: what if wait=true?
            Boolean ready = device.WriteReady;
            if ((!wait) && (!ready)) return false;
            DateTime start = DateTime.Now;
            while (!ready)
            {
                Thread.Sleep(10);
                ready = device.WriteReady;
                if ((DateTime.Now - start) > sIndicatorLag) break;
            }
            if (!ready)
            {
                SetIOHold();
                do Thread.Sleep(20); while (vIOHold && !device.WriteReady);
                ReleaseIOHold();
            }
            return device.Write(word);
        }
    }
}
