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
using System.Threading;

namespace Emulator
{
    class SEL810
    {
        private const Int32 CORE_SIZE = 32768;

        private Object mLock = new Object();
        private Thread mCPUThread;

        private Int16[] mCore = new Int16[CORE_SIZE];
        private Int16 mT, mA, mB, mPC, mIR, mSR, mX, mPPR, mVBR;
        private Boolean mOVF, mCF, mXP;
        private volatile Boolean mHalt = true;
        private volatile Boolean mStep = false;
        
        public SEL810()
        {
            mCPUThread = new Thread(new ThreadStart(CPUThread));
            mCPUThread.Start();
        }

        public SEL810(String imageFile) : this()
        {
            Load(0, imageFile);
        }

        public Boolean IsHalted
        {
            get { return mHalt; } // TOOD: make thread-safe
            set { mHalt = value; } // TODO: make thread-safe
        }

        public Int16 A
        {
            get { return mA; } // TODO: make thread-safe
            set { mA = value; } // TODO: make thread-safe
        }

        public Int16 B
        {
            get { return mB; } // TODO: make thread-safe
            set { mB = value; } // TODO: make thread-safe
        }

        public Int16 PC
        {
            get { return mPC; } // TODO: make thread-safe
            set { mPC = (Int16)(value & 0x7fff); } // TODO: make thread-safe
        }

        public Int16 IR
        {
            get { return mIR; } // TODO: make thread-safe
            set { mIR = value; } // TODO: make thread-safe
        }

        public Int16 this[Int32 index]
        {
            get { return mCore[index]; } // TODO: make thread-safe
            set { mCore[index] = value; } // TODO: make thread-safe
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
                Int16 word = (Int16)(bytesToLoad[offset++] << 8);
                if (count-- > 0) word += bytesToLoad[offset++];
                loadAddress %= CORE_SIZE;
                mCore[loadAddress++] = word;
            }
        }

        public void Start()
        {
            mHalt = false;
        }

        public void Stop()
        {
            mHalt = true;
        }

        public void Step()
        {
            mStep = true;
            while (mStep) Thread.Sleep(50);
        }

        public void Exit()
        {
            mCPUThread.Abort();
            mCPUThread.Join();
        }

        private void CPUThread()
        {
            while (true)
            {
                while ((mHalt) && (!mStep))
                {
                    Thread.Sleep(100);
                }
                if ((mHalt) && (mStep))
                {
                    StepCPU();
                    mStep = false;
                }
                while (!mHalt)
                {
                    StepCPU();
                }
            }
        }

        private void StepCPU()
        {
            // o ooo xim aaa aaa aaa - memory reference instruction
            // o ooo xis sss aaa aaa - augmented instruction
            Int16 r16;
            Int32 r32;
            Int32 ea;
            Int32 op = (mIR >> 12) & 15;
            if (op == 0)
            {
                Int32 aug = mIR & 63;
                Int32 sc = (mIR >> 6) & 15;
                switch (aug)
                {
                    case 0: // HLT - halt
                        mIR = mCore[mPC];
                        SetHalt();
                        return;
                    case 1: // RNA - round A
                        r16 = mA;
                        if ((mB & 0x4000) != 0) r16++;
                        if ((r16 == 0) && (mA != 0)) SetOVF();
                        mA = r16;
                        break;
                    case 2: // NEG - negate A
                        if (mA == -32768) SetOVF();
                        mA = (Int16)(-mA);
                        break;
                    case 3: // CLA - clear A
                        mA = 0;
                        break;
                    case 4: // TBA - transfer B to A
                        mA = mB;
                        break;
                    case 5: // TAB - transfer A to B
                        mB = mA;
                        break;
                    case 6: // IAB - interchange A and B
                        r16 = mA;
                        mA = mB;
                        mB = r16;
                        break;
                    case 7: // CSB - copy sign of B
                        if (mB < 0)
                        {
                            mCF = true; // TODO: find out exactly which instructions CF affects
                            mB &= 0x7fff; // AMA, SMA and NEG are documented, but what else?
                        }
                        break;
                    case 8: // RSA - right shift arithmetic
                        mA = (Int16)((mA & -32768) | (mA >> sc));
                        break;
                    case 9: // LSA - left shift arithmetic
                        r16 = (Int16)(mA & 0x7fff);
                        r16 <<= sc;
                        mA &= -32768;
                        mA |= (Int16)(r16 & 0x7fff);
                        break;
                    case 10: // FRA - full right arithmetic shift
                        r32 = (mA << 16) | ((mB & 0x7fff) << 1);
                        r32 >>= sc;
                        mA = (Int16)(r32 >> 16);
                        mB &= -32768;
                        mB |= (Int16)((r32 >> 1) & 0x7fff);
                        break;
                    case 11: // FLL - full left logical shift
                        r32 = (mA << 16) | (mB & 0xffff);
                        r32 <<= sc;
                        mA = (Int16)((r32 >> 16) & 0xffff);
                        mB = (Int16)(r32 & 0xffff);
                        break;
                    case 12: // FRL - full rotate left
                        Int64 r64 = (mA << 16) | (mB & 0xffff);
                        r64 <<= sc;
                        mA = (Int16)((r64 >> 16) & 0xffff);
                        mB <<= sc;
                        r64 >>= 32;
                        mB |= (Int16)(r64 & ((1 << sc) - 1));
                        break;
                    case 13: // RSL - right shift logical
                        r32 = mA & 0xffff;
                        r32 >>= sc;
                        mA = (Int16)(r32);
                        break;
                    case 14: // LSL - logical shift left
                        mA <<= sc;
                        break;
                    case 15: // FLA - full left arithmetic shift
                        r32 = (mA << 16) | ((mB & 0x7fff) << 1);
                        r32 <<= sc;
                        mA &= -32768;
                        mA |= (Int16)((r32 >> 16) & 0x7fff);
                        mB &= -32768;
                        mB |= (Int16)((r32 >> 1) & 0x7fff);
                        break;
                    case 16: // ASC - complement sign of accumulator
                        mA ^= -32768;
                        break;
                    case 17: // SAS - skip on accumulator sign
                        if (mA >= 0) ++mPC;
                        if (mA == 0) ++mPC;
                        break;
                    case 18: // SAZ - skip if accumulator zero
                        if (mA == 0) ++mPC;
                        break;
                    case 19: // SAN - skip if accumulator negative
                        if (mA < 0) ++mPC;
                        break;
                    case 20: // SAP - skip if accumulator positive
                        if (mA >= 0) ++mPC;
                        break;
                    case 21: // SOF - skip if no overflow
                        if (mOVF) ClearOVF();
                        else ++mPC;
                        break;
                    case 22: // IBS - increment B and skip
                        mB++;
                        if (mB == 0) ++mPC;
                        break;
                    case 23: // ABA - and B and A accumulators
                        mA = (Int16)(mA & mB);
                        break;
                    case 24: // OBA - or B and A accumulators
                        mA = (Int16)(mA | mB);
                        break;
                    case 25: // LCS - load control switches
                        mA = mSR;
                        break;
                    case 26: // SNO - skip normalized accumulator
                        if ((mA & 0x8000) != ((mA << 1) & 0x8000)) ++mPC;
                        break;
                    case 27: // NOP - no operation
                        break;
                    case 28: // CNS - convert number system
                        if (mA == -32768) SetOVF();
                        if (mA < 0) mA = (Int16)(-mA | -32768);
                        break;
                    case 29: // TOI - turn off interrupt
                        // TODO: implement
                        break;
                    case 30: // LOB - long branch
                        mT = mCore[++mPC];
                        mPC = (Int16)((mT & 0x7fff) - 1);
                        break;
                    case 31: // OVS - set overflow
                        SetOVF();
                        break;
                    case 32: // TBP - transfer B to protect register
                        mPPR = mB;
                        break;
                    case 33: // TPB - transfer protect register to B
                        mB = mPPR;
                        break;
                    case 34: // TBV - transfer B to variable base register
                        mVBR = mB;
                        break;
                    case 35: // TVB - transfer variable base register to B
                        mB = mVBR;
                        break;
                    case 36: // STX - store index
                    case 37: // LIX - load index
                        Boolean m = ((mIR & 0x200) != 0); // M flag
                        Boolean i = ((mIR & 0x400) != 0); // I flag
                        if (!i)
                        {
                            ea = ++mPC;
                        }
                        else
                        {
                            mT = mCore[++mPC];
                            Boolean x = ((mT & 0x8000) != 0);
                            i = ((mT & 0x4000) != 0);
                            ea = (mT & 0x3fff) | ((m) ? mPC & 0x4000 : 0);
                            if (x) ea += (mXP) ? mX : mB;
                            while (i)
                            {
                                mT = mCore[mT];
                                x = ((mT & 0x8000) != 0);
                                i = ((mT & 0x4000) != 0);
                                ea = (mT & 0x3fff) | ((m) ? mPC & 0x4000 : 0);
                                if (x) ea += (mXP) ? mX : mB;
                            }
                        }
                        if (aug == 36)
                        {
                            mCore[ea] = mX;
                        }
                        else
                        {
                            mT = mCore[ea];
                            mX = mT;
                        }
                        break;
                    case 38: // XPX - set index pointer to index register
                        mXP = true;
                        break;
                    case 39: // XPB - set index pointer to B
                        mXP = false;
                        break;
                    case 40: // SXB - skip if index register is B
                        if (!mXP) ++mPC;
                        break;
                    case 41: // IXS - increment index and skip if positive
                        mX += (Int16)(sc);
                        if (mX >= 0) ++mPC;
                        break;
                    case 42: // TAX - transfer A to index register
                        mX = mA;
                        break;
                    case 43: // TXA - transfer index register to A
                        mA = mX;
                        break;
                }
            }
            else if (op == 11)
            {
                Int32 aug = (mIR >> 6) & 0x3f;
                Int32 unit = mIR & 0x3f;
                switch (aug)
                {
                    case 4: // SNS - sense numbered switch
                        unit &= 15;
                        if (((mSR << unit) & 0x8000) == 0) ++mPC;
                        break;
                    case 6:
                        if (unit == 0) // PIE - priority interrupt enable
                        {
                            mT = mCore[++mPC];
                            // TODO: implement
                        }
                        else if (unit == 1) // PID - priority interrupt disable
                        {
                            mT = mCore[++mPC];
                            // TODO: implement
                        }
                        break;
                }
            }
            else if (op == 13)
            {

            }
            else
            {
                ea = mIR & 511;
                if ((mIR & 0x200) != 0) ea |= mPC & 0x7e00; // M flag
                Boolean i = ((mIR & 0x400) != 0); // I flag
                Boolean x = ((mIR & 0x800) != 0); // X flag
                if (x) ea += (mXP) ? mX : mB;
                while (i)
                {
                    mT = mCore[ea];
                    i = ((mT & 0x4000) != 0);
                    x = ((mT & 0x8000) != 0);
                    ea = (mPC & 0x4000) | (mT & 0x3fff);
                    if (x) ea += (mXP) ? mX : mB;
                }
                switch (op)
                {
                    case 1: // LAA - load A accumulator
                        mT = mCore[ea];
                        mA = mT;
                        break;
                    case 2: // LBA - load B accumulator
                        mT = mCore[ea];
                        mB = mT;
                        break;
                    case 3: // STA - store A accumulator
                        mCore[ea] = mA;
                        break;
                    case 4: // STB - store B accumulator
                        mCore[ea] = mB;
                        break;
                    case 5: // AMA - add memory to A
                        mT = mCore[ea];
                        r16 = (Int16)(mA + mT);
                        if (((mA & 0x8000) == (mT & 0x8000)) && ((mA & 0x8000) != (r16 & 0x8000))) SetOVF();
                        mA = r16;
                        break;
                    case 6: // SMA - subtract memory from A
                        mT = mCore[ea];
                        r16 = (Int16)(mA - mT);
                        if (((mA & 0x8000) != (mT & 0x8000)) && ((mA & 0x8000) != (r16 & 0x8000))) SetOVF();
                        mA = r16;
                        break;
                    case 7: // MPY - multiply
                        mT = mCore[ea];
                        r32 = mT * mB;
                        if ((mT == -32768) && (mB == -32768)) SetOVF();
                        mB = (Int16)(r32 & 0x7fff);
                        mA = (Int16)((r32 >> 15) & 0xffff);
                        break;
                    case 8: // DIV - divide
                        mT = mCore[ea];
                        r32 = (mA <<  15) | (mB & 0x7fff);
                        if (mA >= mT) SetOVF();
                        mB = (Int16)(r32 % mT);
                        mA = (Int16)(r32 / mT);
                        break;
                    case 9: // BRU - branch unconditional
                        mPC = (Int16)(ea - 1);
                        break;
                    case 10: // SPB - store place and branch
                        mCore[ea] = ++mPC;
                        mPC = (Int16)(ea);
                        break;
                    case 12: // IMS - increment memory and skip
                        mT = mCore[ea]++;
                        if (mCore[ea] == 0) ++mPC;
                        break;
                    case 13: // CMA - compare memory and accumulator
                        mT = mCore[ea];
                        if (mA >= mT) ++mPC;
                        if (mA == mT) ++mPC;
                        break;
                    case 14: // AMB - add memory to B
                        mT = mCore[ea];
                        r16 = (Int16)(mT + mB);
                        if (((mA & 0x8000) == (mT & 0x8000)) && ((mA & 0x8000) != (r16 & 0x8000))) SetOVF();
                        mB = r16;
                        break;
                }
            }
            if (mIR != 7) mCF = false;
            mIR = mCore[++mPC];
        }

        private void SetHalt()
        {
            mHalt = true;
        }

        private void SetOVF()
        {
            mOVF = true;
        }

        private void ClearOVF()
        {
            mOVF = false;
        }

        private void SetCF()
        {
            mCF = true;
        }

        private void ClearCF()
        {
            mCF = false;
        }
    }
}
