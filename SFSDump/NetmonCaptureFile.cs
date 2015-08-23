using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.NetworkMonitor;

namespace SFSDump
{
    public class NetmonInit
    {
        public static readonly NetmonInit Default = new NetmonInit();

        public bool IsInited { get; set; }

        private NetmonInit()
        {
            IsInited = InitializeNMAPI();
        }

        ~NetmonInit()
        {
            if (IsInited)
                CloseNMAPI();
            IsInited = false;
        }

        private bool InitializeNMAPI()
        {
            // Initialize the NMAPI          
            NM_API_CONFIGURATION apiConfig = new NM_API_CONFIGURATION();
            apiConfig.Size = (ushort)System.Runtime.InteropServices.Marshal.SizeOf(apiConfig);
            ulong errno = NetmonAPI.NmGetApiConfiguration(ref apiConfig);
            if (errno != 0)
            {
                Console.WriteLine("Failed to Get NMAPI Configuration Error Number = " + errno);
                return false;
            }

            // Set possible configuration values for API Initialization Here
            ////apiConfig.CaptureEngineCountLimit = 4;

            errno = NetmonAPI.NmApiInitialize(ref apiConfig);
            if (errno != 0)
            {
                Console.WriteLine("Failed to Initialize the NMAPI Error Number = " + errno);
                return false;
            }

            return true;
        }

        private void CloseNMAPI()
        {
            ulong errno = NetmonAPI.NmApiClose();
            if (errno != 0)
            {
                Console.WriteLine("Error unloading NMAPI Error Number = " + errno);
            }
        }
    }
    public class NetmonNplParser
    {
        private IntPtr _handle;
        private uint _ret;

        public static readonly NetmonNplParser Default = new NetmonNplParser();

        private static ParserCallbackDelegate pErrorCallBack = new ParserCallbackDelegate(ParserCallback);

        private NetmonNplParser()
        {
            _ret = NetmonAPI.NmLoadNplParser(null, NmNplParserLoadingOption.NmAppendRegisteredNplSets, pErrorCallBack, IntPtr.Zero,
                out _handle);
        }
        ~NetmonNplParser()
        {
            if (_handle != IntPtr.Zero)
                NetmonAPI.NmCloseHandle(_handle);
        }

        public IntPtr Handle
        {
            get
            {
                return _handle;
            }
        }

        public static void ParserCallback(IntPtr pCallerContext,
                                                UInt32 ulStatusCode,
                                                string lpDescription,
                                                NmCallbackMsgType ulType)
        {
            if (ulType == NmCallbackMsgType.Error)
            {
                Console.WriteLine("ERROR: " + lpDescription);
            }
            else
            {
                Console.WriteLine(lpDescription);
            }
        }
    }

    public class NetMonFrameParser
    {
        private IntPtr _FrameParserConfig;
        private IntPtr _FrameParser;
        private Dictionary<string, uint> FieldIds = new Dictionary<string, uint>();
        private Dictionary<string, uint> PropertyIds = new Dictionary<string, uint>();

        private Boolean NeedsRebuild = true;

        private static ParserCallbackDelegate pErrorCallBack = new ParserCallbackDelegate(ParserCallback);

        public NetMonFrameParser()
        {
            _FrameParserConfig = IntPtr.Zero;
        }

        ~NetMonFrameParser()
        {
            if (_FrameParserConfig != IntPtr.Zero)
                NetmonAPI.NmCloseHandle(_FrameParserConfig);
            _FrameParserConfig = IntPtr.Zero;

            if (_FrameParser != IntPtr.Zero)
                NetmonAPI.NmCloseHandle(_FrameParser);
            _FrameParser = IntPtr.Zero;
            
        }
        private IntPtr FrameParserConfig
        {
            get
            {
                if (_FrameParserConfig == IntPtr.Zero)
                {
                    NetmonAPI.NmCreateFrameParserConfiguration(NetmonNplParser.Default.Handle, pErrorCallBack, IntPtr.Zero, out _FrameParserConfig);
                }
                return _FrameParserConfig;
            }
        }

        public void AddFiled(string name)
        {
            if (FieldIds.ContainsKey(name))
                return;
            uint id;
            if (NetmonAPI.NmAddField(this.FrameParserConfig, name, out id) == 0)
            {
                FieldIds[name] = id;
                NeedsRebuild = true;
            }
        }

        public void AddProperty(string name)
        {
            if (PropertyIds.ContainsKey(name))
                return;
            uint id;
            if (NetmonAPI.NmAddProperty(this.FrameParserConfig, name, out id) == 0)
            {
                PropertyIds[name] = id;
                NeedsRebuild = true;
            }
        }

        public uint Field(string name)
        {
            return FieldIds[name];
        }

        public bool HasField(string name)
        {
            return FieldIds.ContainsKey(name);
        }

        public uint Property(string name)
        {
            return PropertyIds[name];
        }

        public bool HasProperty(string name)
        {
            return PropertyIds.ContainsKey(name);
        }

        public IntPtr Handle
        {
            get
            {
                if (NeedsRebuild)
                {
                    if (_FrameParser != IntPtr.Zero)
                        NetmonAPI.NmCloseHandle(_FrameParser);

                    NetmonAPI.NmCreateFrameParser(FrameParserConfig, out _FrameParser, NmFrameParserOptimizeOption.ParserOptimizeNone);
                    NeedsRebuild = false;
                }
                return _FrameParser;
            }
        }
       

        public static void ParserCallback(IntPtr pCallerContext,
                                                UInt32 ulStatusCode,
                                                string lpDescription,
                                                NmCallbackMsgType ulType)
        {
            if (ulType == NmCallbackMsgType.Error)
            {
                Console.WriteLine("ERROR: " + lpDescription);
            }
            else
            {
                Console.WriteLine(lpDescription);
            }
        }
    }
    public class NetmonFrame
    {
        private IntPtr _RawFrame = IntPtr.Zero;
        private IntPtr _ParsedFrame = IntPtr.Zero;

        private NetmonCaptureFile _file;
        private uint _index;
        public byte[] _buffer;
        public NetmonFrame(NetmonCaptureFile file, uint index)
        {
            _file = file;
            _index = index;
        }

        public NetMonFrameParser Parser { get; set; }

        private IntPtr RawFrame
        {
            get
            {
                if (_RawFrame == IntPtr.Zero)
                {
                    NetmonAPI.NmGetFrame(_file.Handle, _index, out _RawFrame);
                }
                return _RawFrame;
                
            }
        }

        private IntPtr ParsedFrame
        {
            get
            {
                if (this.Parser == null)
                    return IntPtr.Zero;

                if (_ParsedFrame == IntPtr.Zero)
                {
                    IntPtr hIFrame;
                    NetmonAPI.NmParseFrame(Parser.Handle, this.RawFrame, _index,
                        NmFrameParsingOption.FieldFullNameRequired | NmFrameParsingOption.DataTypeNameRequired,
                        out _ParsedFrame, out hIFrame);
                }
                return _ParsedFrame;
            }
        }

        public byte[] GetFieldBuffer(string name)
        {
            if (this.Parser == null)
                return null;

            if (!this.Parser.HasField(name))
                return null;

            uint offset, bsize;
            uint fieldId = this.Parser.Field(name);
            if (0 == NetmonAPI.NmGetFieldOffsetAndSize(ParsedFrame, fieldId, out offset, out bsize))
            {
                if (bsize > 0)
                {
                    uint size = bsize / 8;
                    byte[] bytes = new byte[bsize / 8];
                    UInt32 uRet;
                    unsafe
                    {
                        fixed (byte* ptr = bytes)
                        {
                            if (0 == NetmonAPI.NmGetFieldInBuffer(ParsedFrame, fieldId, size, ptr, out uRet))
                            {
                                return bytes;
                            }
                        }
                    }
                }

            }
            return null;
        }

        public byte[] Buffer
        {
            get
            {
                if (_buffer == null)
                {
                    uint uLength;
                    if (0 == NetmonAPI.NmGetRawFrameLength(RawFrame, out uLength))
                    {
                        _buffer = new byte[uLength];
                        unsafe
                        {
                            fixed (byte* ptr = _buffer)
                            {
                                uint outLength;
                                if (0 != NetmonAPI.NmGetRawFrame(RawFrame, uLength, ptr, out outLength))
                                {
                                    _buffer = null;
                                }
                            }
                        }
                        
                    }                  
                    
                }
                return _buffer;
            }
        }

        public byte GetFieldByte(string name)
        {
            if (this.Parser == null)
                return 0;

            if (!this.Parser.HasField(name))
                return 0;

            uint offset, bsize;
            uint fieldId = this.Parser.Field(name);
            if (0 == NetmonAPI.NmGetFieldOffsetAndSize(ParsedFrame, fieldId, out offset, out bsize))
            {
                if (bsize == 8)
                {
                    byte uRet;
                    if (0 == NetmonAPI.NmGetFieldValueNumber8Bit(ParsedFrame, fieldId, out uRet))
                    {
                        return uRet;
                    }

                }

            }
            return 0;
        }

        public uint GetPropertyUint(string name)
        {
            if (this.Parser == null)
                return 0;

            if (!this.Parser.HasProperty(name))
                return 0;

            uint offset, bsize;
            uint fieldId = this.Parser.Property(name);

            uint uRet, ulReturnLength;
            NmPropertyValueType vt;
            unsafe
            {
                byte[] uintBuffer = new byte[8];
                fixed (byte* pbuf = uintBuffer)
                {
                    if (0 == NetmonAPI.NmGetPropertyById(Parser.Handle, fieldId, 8, pbuf, out ulReturnLength, out vt, 0, null))
                    {
                        //uintBuffer.to
                        return System.BitConverter.ToUInt32(uintBuffer, 0);
                    }
                }
            }

            return 0;
        }
    }
    public class NetmonCaptureFile
    {
        private IntPtr _fileHandle;
        private uint frameCount = 0;

        public NetmonCaptureFile(string file)
        {
            NetmonAPI.NmOpenCaptureFile(file, out _fileHandle);
            
            NetmonAPI.NmGetFrameCount(this.Handle, out frameCount);
        }

        ~NetmonCaptureFile()
        {
            if (_fileHandle != IntPtr.Zero)
                NetmonAPI.NmCloseHandle(_fileHandle);
        }

        public IntPtr Handle
        {
            get {
                return _fileHandle;
            }            
        }

        public uint FrameCount
        {
            get
            {              
                return frameCount;
            }
        }

        public NetmonFrame GetFrame(uint index)
        {
            return new NetmonFrame(this, index);
        }


    }
}
