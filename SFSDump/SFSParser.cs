using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SFSDump
{
    class SFSParser
    {
        private Sfs2X.Bitswarm.PendingPacket PendingPacket; 

        private bool NeedsMoreData = false;
        private SortedDictionary<uint, Sfs2X.Util.ByteArray> PeddingBuffer
            = new SortedDictionary<uint,Sfs2X.Util.ByteArray>();
        private uint NextSeq = 0;

        private bool IsReq = false;

        private System.IO.StreamWriter Writer;
        public SFSParser(bool req, System.IO.StreamWriter sw)
        {
            IsReq = req;
            Writer = sw;
        }

        public void ReadBuffer(byte[] buffer, uint seq)
        {
            Sfs2X.Util.ByteArray data = new Sfs2X.Util.ByteArray(buffer);
            data.Position = 0;

            if (NextSeq == 0 || NextSeq == seq)
            {
                NextSeq = seq + (uint)data.Length;
                if (!NeedsMoreData)
                    HandleNewPacket(data);
                else
                    HandleContinuePacket(data);

                

                HandlePaddingPacket();
            }
            else
            {
                PeddingBuffer[seq] = data;
            }            
        }               

        private void RendRespBuffer(byte[] buffer, uint seq)
        {
            Sfs2X.Util.ByteArray data = new Sfs2X.Util.ByteArray(buffer);
            data.Position = 0;

            if (NextSeq == 0 || NextSeq == seq)
            {
                NextSeq += (uint)data.Length;
                if (!NeedsMoreData)
                    HandleNewPacket(data);
                else
                    HandleContinuePacket(data);

                HandlePaddingPacket();
            }
            else
            {
                PeddingBuffer[seq] = data;
            }
        }

        private void HandlePaddingPacket()
        {
            if (PeddingBuffer.Count > 0)
            {
                var item = PeddingBuffer.FirstOrDefault();
                if (item.Key == NextSeq)
                {
                    Sfs2X.Util.ByteArray data = item.Value;
                    PeddingBuffer.Remove(item.Key);

                    NextSeq += (uint)data.Length;
                    if (!NeedsMoreData)
                        HandleNewPacket(data);
                    else
                        HandleContinuePacket(data);

                    HandlePaddingPacket();
                }
            }
            
        }

        private void HandleNewPacket(Sfs2X.Util.ByteArray data)
        {
            Byte headerByte = data.ReadByte();

            PendingPacket = new Sfs2X.Bitswarm.PendingPacket(Sfs2X.Core.PacketHeader.FromBinary(headerByte));

            int dataSize;
            if (PendingPacket.Header.BigSized)
            {
               dataSize = data.ReadInt();
            }
            else
            {
                dataSize = data.ReadUShort();
            }

            data = ResizeByteArray(data, (uint)data.Position);

            PendingPacket.Header.ExpectedLength = dataSize;

            HandleContinuePacket(data);           
        }

        private void HandleContinuePacket(Sfs2X.Util.ByteArray data)
        {
            int packetRemain = PendingPacket.Header.ExpectedLength - PendingPacket.Buffer.Length;
            if (packetRemain > data.Length)
            {//还有包
                PendingPacket.Buffer.WriteBytes(data.Bytes);
                NeedsMoreData = true;
            }
            else
            {
                PendingPacket.Buffer.WriteBytes(data.Bytes, 0, packetRemain);

                if (PendingPacket.Header.Compressed)
                {
                    PendingPacket.Buffer.Uncompress();
                }

                Sfs2X.Entities.Data.SFSObject obj = Sfs2X.Entities.Data.SFSObject.NewFromBinaryData(PendingPacket.Buffer);
                string dumpstr = obj.GetDump();
//                 Console.WriteLine(IsReq ? "req:" : "resp");
//                 Console.WriteLine(dumpstr);

                Writer.WriteLine(IsReq ?    "req:==========================" 
                                        :   "resp:=========================");
                Writer.WriteLine(dumpstr);

                int dataRemain = data.Length - packetRemain;
                if (dataRemain == 0)
                    NeedsMoreData = false;
                else
                {
                    data = ResizeByteArray(data, (uint)packetRemain);
                    HandleNewPacket(data);
                }
            }
        }

        private Sfs2X.Util.ByteArray ResizeByteArray(Sfs2X.Util.ByteArray data, uint pos)
        {
            int len = data.Length - (int)pos;
            byte[] b = new byte[(uint)len];
            Array.ConstrainedCopy(data.Bytes, (int)pos, b, 0, len);
            return new Sfs2X.Util.ByteArray(b);
        }
    }
}
