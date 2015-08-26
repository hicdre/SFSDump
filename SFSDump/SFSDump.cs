using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.NetworkMonitor;
using Sfs2X;
using System.IO;

namespace SFSDump
{
    public class SFSDumper
    {
        public static bool DumpFile(string fileName)
        {
            if (NetmonInit.Default.IsInited)
            {
                return DumpFileInternal(fileName);
            }
            return false;
        }
        private static bool DumpFileInternal(string fileName)
        {
            if (fileName == null)
                return false;
            NetmonCaptureFile file = new NetmonCaptureFile(fileName);
            FileStream fs = new FileStream(System.IO.Path.ChangeExtension(fileName, ".txt"), FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);
            SFSParser respParser = new SFSParser(false, sw);
            SFSParser reqParser = new SFSParser(true, sw);
            NetMonFrameParser parser = new NetMonFrameParser();
            parser.AddFiled("TCP.TCPPayload.TCPPayloadData");
            parser.AddFiled("TCP.SrcPort");
            parser.AddFiled("TCP.DstPort");
            parser.AddFiled("TCP.Flags");
            parser.AddFiled("IPv4.SourceAddress");
            parser.AddProperty("Property.TCPSeqNumber");
            parser.AddProperty("Property.TCPCheckSumStatus");

            for (uint i = 0; i < file.FrameCount; i++)
            {
                NetmonFrame frame = file.GetFrame(i);
                frame.Parser = parser;

                byte[] address = frame.GetFieldBuffer("IPv4.SourceAddress");

                if (address == null)
                    continue;                                               

                bool isReq = (address[0] == 10 && address[1] == 10);

                if (!isReq)
                {
                    //某些情况下，请求的包的checksum交由网卡计算，导致此时抓包获取的checksum不正确，此时请求包应放行
                    string checksum = frame.GetPropertyString("Property.TCPCheckSumStatus");
                    if (checksum == "Bad")
                        continue;
                }
                

                {
                    byte flags = frame.GetFieldByte("TCP.Flags");

                    if ((flags & 0x02) == 0x02)//SYN
                    {
                        uint seq = frame.GetPropertyUint("Property.TCPSeqNumber");
                        if (isReq)
                            reqParser.Seq = seq + 1;
                        else
                            respParser.Seq = seq + 1;
                    }                    
                }

                {
                    byte[] buffer = frame.GetFieldBuffer("TCP.TCPPayload.TCPPayloadData");
                    if (buffer != null)
                    {
                        uint seq = frame.GetPropertyUint("Property.TCPSeqNumber");                        

                        if (isReq)
                            reqParser.ReadBuffer(buffer, seq);
                        else
                            respParser.ReadBuffer(buffer, seq);
                    }
                }

            }
            sw.Close();
            fs.Close();
            file.Close();
            return true;
        }
    }
}
