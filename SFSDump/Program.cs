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

    class Program
    {       
        static void Main(string[] args)
        {
            if (NetmonInit.Default.IsInited)
            {
                Program.DumpFile(args[0]);
            }            
        }

        public static void DumpFile(string fileName)
        {
            if (fileName == null)
                return;
            NetmonCaptureFile file = new NetmonCaptureFile(fileName);
            FileStream fs = new FileStream(System.IO.Path.ChangeExtension(fileName, ".txt"), FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);
            SFSParser respParser = new SFSParser(false, sw);
            SFSParser reqParser = new SFSParser(true, sw);
            NetMonFrameParser parser = new NetMonFrameParser();
            parser.AddFiled("TCP.TCPPayload.TCPPayloadData");   
            parser.AddFiled("TCP.SrcPort");
            parser.AddFiled("TCP.DstPort");
            parser.AddFiled("IPv4.SourceAddress");
            parser.AddProperty("Property.TCPSeqNumber");
            
            for(uint i = 0; i < file.FrameCount; i++)
            {
                NetmonFrame frame = file.GetFrame(i);
                frame.Parser = parser;
               
                {
                    byte[] buffer = frame.GetFieldBuffer("TCP.TCPPayload.TCPPayloadData");
                    if (buffer != null)
                    {                       

//                         var packet = PacketDotNet.Packet.ParsePacket(PacketDotNet.LinkLayers.Ethernet, frame.Buffer);
//                         var tcpPacket = PacketDotNet.TcpPacket.GetEncapsulated(packet);
//                         if (tcpPacket != null)
//                         {
//                             if (tcpPacket.PayloadData != null)
//                             {
//                                 var ipPacket = (PacketDotNet.IpPacket)tcpPacket.ParentPacket;
//                                 System.Net.IPAddress srcIp = ipPacket.SourceAddress;
//                                 System.Net.IPAddress dstIp = ipPacket.DestinationAddress;
//                                 int srcPort = tcpPacket.SourcePort;
//                                 int dstPort = tcpPacket.DestinationPort;
// 
//                                 bool isReq = srcIp.ToString().StartsWith("192.168");
//                                 if (isReq)
//                                     reqParser.ReadBuffer(tcpPacket.PayloadData, tcpPacket.SequenceNumber);
//                                 else
//                                     respParser.ReadBuffer(tcpPacket.PayloadData, tcpPacket.SequenceNumber);
//                             }
//                         }
                        uint seq = frame.GetPropertyUint("Property.TCPSeqNumber");

                        byte[] address = frame.GetFieldBuffer("IPv4.SourceAddress");

                        bool isReq = (address[0] == 192 && address[1] == 168);

                        if (isReq)
                            reqParser.ReadBuffer(buffer, seq);
                        else
                            respParser.ReadBuffer(buffer, seq);
                    }
                }

            }
            sw.Close();
            fs.Close();
            
        }

       
    }
}
