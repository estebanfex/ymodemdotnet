using System;
using System.Collections.Generic;
using System.Text;

namespace Curit.Module.RTX.Com
{
    class Packet
    {
        public ushort packetnum { get; set; }
        public bool longpacket { get; set; }
        public bool isinit { get; set; }
        public bool isend { get; set; } 
        public string filename { get; set; }
        public int filelength { get; set; }
        public byte[] packet { get; private set; }
        public byte[] data { get; set; }
        private byte[] innerdata;
        private byte[] header = new byte[3];
        private byte[] crcfooter = new byte[2];
        private Crc16Ccitt crc = new Crc16Ccitt(InitialCrcValue.Zeros);
        public byte[] innerData
        {
            get { return innerdata; }
            set { innerdata = value; }
        }
        
        public void createPacket()
        {
            if (longpacket)
            {
                header[0] = 0x02;
                packet = new byte[1029];
                innerdata = new byte[1024];
            }
            else
            {
                header[0] = 0x01;
                packet = new byte[133];
                innerdata = new byte[128];
            }

            if (isinit)
            {
                if (packetnum != 0) 
                    throw new Exception("init can only be first packet so 0");
                else if (filename.Length > 1023)
                    throw new Exception("filename is to large");
                else
                {
                    header[1] = (byte)packetnum;
                    header[2] = (byte)(0xFF - packetnum);
                    Buffer.BlockCopy(ChartoByteArray(filename.ToCharArray()), 0, innerdata,0, filename.Length);
                    Buffer.BlockCopy(ChartoByteArray(filelength.ToString().ToCharArray()), 0, innerdata, filename.Length + 1, filelength.ToString().ToCharArray().Length);                    
                    Buffer.BlockCopy(innerdata, 0, packet, 3, innerdata.Length);
                    crcfooter = crc.ComputeChecksumBytes(innerdata);
                    //char array of int of filelength and copyt to arr offset is length of filename + length of header + one byte of nothing. 
                    Buffer.BlockCopy(header, 0, packet, 0, 3);
                    Buffer.BlockCopy(crcfooter, 0, packet, packet.Length - 2, 2);
                }
            }
            else if (isend)
            {
                if (packetnum != 0)
                    throw new Exception("end can only have packetnum 0");

                Buffer.BlockCopy(data, 0, innerdata, 0, data.Length);
                header[1] = (byte)packetnum;
                header[2] = (byte)(0xFF - packetnum);
                crcfooter = crc.ComputeChecksumBytes(innerdata);
                Buffer.BlockCopy(innerdata, 0, packet, 3, 128);
                Buffer.BlockCopy(header, 0, packet, 0, 3);
                Buffer.BlockCopy(crcfooter, 0, packet, packet.Length - 2, 2);
            }
            else if (packetnum == 0)
            {
                throw new Exception("data can't be in packet one");
            }
            else
            {
                Buffer.BlockCopy(data, 0, innerdata, 0, data.Length);
                header[1] = (byte)packetnum;
                header[2] = (byte)(0xFF - packetnum);
                crcfooter = crc.ComputeChecksumBytes(innerdata);
                Buffer.BlockCopy(innerdata, 0, packet, 3, 1024);
                Buffer.BlockCopy(header, 0, packet, 0, 3);
                Buffer.BlockCopy(crcfooter, 0, packet, packet.Length - 2, 2);
            }
        }

        private byte[] ChartoByteArray(char[] input)
        {
            byte[] output = new byte[input.Length];
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = (byte)input[i];
            }
            return output;
        }
    }
}
