using System;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using System.IO.Ports;
using System.IO;
using System.Windows.Forms;

namespace Curit.Module.RTX.Com
{
    public enum SYSTEMSTATUS 
    {
        STARTED = 0,
        STOPPED = 1
    }

    public class LegacyCommunication : IDisposable
    {
        private SerialPort sp;
        private SYSTEMSTATUS systemstatus;
        private Boolean loggedin = false;
        private byte[] inputdata = new byte[65535];
        private int bytesreceived = 0;
        private bool readbinary = false;
        private bool _waitforack;
        private bool newline;
        private bool oplevel;
        private bool uslevel;
        private StringBuilder sb = new StringBuilder();
        private List<string> linehistory = new List<string>();
        public delegate void Progress(int curpos, int max);
        public delegate void NewLine(String s);
        private NewLine newLine;
        public Progress progressUpdate;

        public LegacyCommunication(String comport, NewLine newLineDelegate) : this(comport)
        {
            newLine += newLineDelegate;
        }
 
        public LegacyCommunication(String comport)
        {
            sp = new SerialPort(comport);
            newLine += new NewLine(errorCheck);
            try
            {
                sp.Open();
                sp.RtsEnable = true;
                sp.StopBits = System.IO.Ports.StopBits.One;
                sp.DataBits = 8;
                sp.BaudRate = 115200;
                sp.ReceivedBytesThreshold = 1;
                sp.DataReceived += new SerialDataReceivedEventHandler(sp_DataReceived);
                systemstatus = SYSTEMSTATUS.STARTED;
                
                testSystemStatus();
            }
            catch (Exception ex) 
            {
                throw new Exception("Poort con niet geopend worden.", ex);
            }
        }

        private void errorCheck(string templine)
        {
            if (templine.Contains("ERROR - Wrong login level or unknown command"))
            {
                throw new Exception("ERROR - Wrong login level or unknown command.");
            }
            else if (templine.Contains("ERROR - Already locked"))
            {
                throw new Exception("ERROR - Already locked.");
            }
            else if (templine.Contains("ERROR - Not locked"))
            {
                throw new Exception("ERROR - Not locked.");
            }
            else if (templine.Contains("ERROR SAVING DATA IN DATABASE"))
            {
                throw new Exception("ERROR SAVING DATA IN DATABASE");
            }
            else if (templine.Contains("Syntax error"))
            {
                throw new Exception("Syntax error");
            }
            else if (templine.Contains("not permitted"))
            {
                throw new Exception("Something likely a devicetype is not permitted");
            }
            else if (templine.Contains("ERROR - Only one generic Bluetooth device permitted"))
            {
                throw new Exception("ERROR - Only one generic Bluetooth device permitted");
            }
            else if (templine.Contains("ERROR - Only one IR device permitted"))
            {
                throw new Exception("ERROR - Only one IR device permitted");
            }
            else if (templine.Contains("ERROR - Only one RS232 device permitted"))
            {
                throw new Exception("ERROR - Only one RS232 device permitted");
            }
            else if (templine.Contains("DEVICE NOT IN DATABASE"))
            {
                throw new Exception("DEVICE NOT IN DATABASE");
            }
            else if (templine.Contains("ERROR - Names must be a maximum of 10 chars"))
            {
                throw new Exception("ERROR - Names must be a maximum of 10 chars");
            }
            else if (templine.Contains("ERROR - Not a valid mode"))
            {
                throw new Exception("ERROR - Not a valid mode.");
            }
            else if (templine.Contains("ERROR – String not accepted"))
            {
                throw new Exception("ERROR – String not accepted.");
            }
            else if (templine.Contains("ERROR – syntax error"))
            {
                throw new Exception("ERROR – syntax error");
            }
            else if (templine.Contains("Syntax error - Name must be 2 to 15 characters long"))
            {
                throw new Exception("Syntax error - Name must be 2 to 15 characters long.");
            }
            else if (templine.Contains("Syntax Error. Not a FQHN"))
            {
                throw new Exception("Syntax Error. Not a FQHN");
            }
        }

        private void testSystemStatus()
        {
            sendNewLine();
            waitforline();
            if (linehistory[0].Contains("Operator Level"))
            {
                loggedin = true;
                oplevel = true;
            }
            else if (linehistory[0].Contains("User Level"))
            {
                uslevel = true;
                loggedin = true;
            }
            else if (linehistory[0].Contains("User Name"))
            {
                loggedin = false;
            }
            else if (linehistory[0].Contains("Password"))
            {
                loggedin = false;
                sp.Write("goaway");
                sendNewLine();
            }
            return;
        }

        void sp_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (this.readbinary)
            {
                while (sp.BytesToRead != 0)
                {
                    if (bytesreceived == 65534)
                    {
                        inputdata = new byte[65535];
                        bytesreceived = 0;
                    }
                    sp.Read(inputdata, bytesreceived, 1);
                    if (inputdata[bytesreceived] == (byte)0x06)
                        _waitforack = true;
                    else
                        _waitforack = false;

                    if (_waitforack == true && inputdata[bytesreceived] == (byte)0x15)
                    {
                        //moet nog resend last package worden.
                        char[] tca = new char[1];
                        tca[0] = (char)0x18;
                        sp.Write(tca, 0, 1);
                        throw new Exception("Filesend failed!");
                    }
                    bytesreceived++;
                }
            }
            else
            {
                char[] tmpchar = new char[1];
                while (sp.BytesToRead != 0)
                {
                    sp.Read(tmpchar, 0, 1);
                    sb.Append(tmpchar[0]);
                    if ((tmpchar[0] == (char)0x3E))// || (tmpchar[0] == (char)0x0D))
                    {
                        newLine(sb.ToString());
                        linehistory.Add(sb.ToString());
                        sb = new StringBuilder();
                        newline = true;
                    }
                }
            }
        }

        public void sendConfig(String config)
        {
            sendConfig(config.Split(new string[] { Environment.NewLine },StringSplitOptions.RemoveEmptyEntries));
        }

        public void sendConfig(String[] config)
        {
            foreach (String s in config)
            {
                sp.Write(s);
                sendNewLine();
                waitforline();
            }
        }

        public void sendBinaryFile(String Path)
        {
            if (oplevel && !uslevel)
            {
                FileStream fs = new FileStream(Path, FileMode.Open);
                BinaryReader br = new BinaryReader(fs);
                MemoryStream ms = new MemoryStream();
                BinaryWriter bw = new BinaryWriter(ms);
                byte[] bytes = new byte[br.BaseStream.Length]; ;
                while (br.BaseStream.Position != br.BaseStream.Length)
                {
                    bw.Write(br.ReadBytes(1024));
                    bw.Flush();
                }
                fs.Close();
                br.Close();
                bytes = ms.ToArray();
                ms.Close();
                bw.Flush();
                bw.Close();
                sendBinaryFile(bytes, System.IO.Path.GetFileName(Path));
            }
        }

        public void sendBinaryFile(byte[] bytes,string filename)
        {
            readbinary = true;
            sp.Write("AT+pBINARYUPLOAD");
            sendNewLine();
            waitfor('C');
            ushort packetnum = 0;

            Packet initpacket = new Packet();
            initpacket.isinit = true;
            initpacket.packetnum = packetnum;
            initpacket.filename = filename;
            initpacket.filelength = bytes.Length;
            
            if (filename.Length > 125)
                initpacket.longpacket = true;
            else
                initpacket.longpacket = false;

            initpacket.createPacket();

            sp.Write(initpacket.packet, 0, initpacket.packet.Length);

            waitforack();
            waitfor('C');

            MemoryStream ms = new MemoryStream(bytes);
            byte[] temparr = new byte[1024];
            Packet sendPacket;
            long numpack = Math.Abs(((long)ms.Length) / ((long)1024));
            

            //long leftover = ms.Length % 1024;
            BinaryReader br = new BinaryReader(ms);

            while (ms.Position != ms.Length)
            {
                progressUpdate(packetnum, Convert.ToInt32(numpack));
                _waitforack = false;
                packetnum++;
                temparr = br.ReadBytes(1024);
                sendPacket = new Packet();
                sendPacket.packetnum = packetnum;
                sendPacket.longpacket = true;
                sendPacket.isinit = false;
                sendPacket.data = temparr;
                sendPacket.createPacket();
                sp.Write(sendPacket.packet, 0, sendPacket.packet.Length);
                waitforack();
            }
            sendEndOftransmision();
            waitforack();
            readbinary = false;
            waitforline();

        }

        private void sendEndOftransmision()
        {
            sp.Write(new byte[] { (byte)0x04 }, 0, 1);
            Packet endpacket = new Packet();
            endpacket.isend = true;
            endpacket.longpacket = false;
            endpacket.packetnum = 0;
            endpacket.data = new byte[128];
            endpacket.createPacket();
            sp.Write(endpacket.packet, 0, endpacket.packet.Length);
            return;
        }

        private void waitforack()
        {
            while (!_waitforack)
            {
                Application.DoEvents();
            }
            return;
        }

        private void waitfor(char p)
        {
            if (bytesreceived != 0)
            {
                while ((char)inputdata[bytesreceived - 1] != p)
                    Application.DoEvents();
            }
            else
            {
                Application.DoEvents();
            }
            return;
        }

        public void login(string username, string password) 
        {
            if (!loggedin)
            {
                sp.Write(username);
                sendNewLine();
                waitforline();

                if (linehistory[linehistory.Count - 1].Contains("Password>"))
                {
                    sp.Write(password);
                    sendNewLine();
                    waitforline();
                    if (linehistory[linehistory.Count - 1].Contains("User Level>"))
                    {
                        uslevel = true;
                        loggedin = true;
                    }
                    else if (linehistory[linehistory.Count - 1].Contains("Operator Level>"))
                    {
                        oplevel = true;
                        loggedin = true;
                    }
                    else
                    {
                        throw new Exception("Password fout");
                    }
                }
                else
                {
                    throw new Exception("Username fout");
                }
            }
        }

        private void waitforline()
        {
            while (!newline)
                Application.DoEvents();

            newline = false;
        }

        private void wait(bool p)
        {
            while (!p)
                Application.DoEvents();
        }

        public void stopSystem()
        {
            
            if (this.systemstatus == SYSTEMSTATUS.STARTED)
            {
                sp.Write("AT+pSTOP_SYSTEM");
                sendNewLine();
            }
            else
            {
                throw new Exception("System was al gestopt");
            }
        }

        private void sendNewLine()
        {
            sp.Write(new byte[] { (byte)0x0D }, 0, 1);
            return;
        }

        public void logout()
        {
            sp.Write("logout");
            sendNewLine();
            waitforline();
            waitforline();
        }

        public void startSystem()
        {
            if (this.systemstatus == SYSTEMSTATUS.STARTED)
            {
                sp.Write("AT+pSTART_SYSTEM");
                sendNewLine();
            }
            else
            {
                throw new Exception("System was al gestopt");
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (systemstatus == SYSTEMSTATUS.STOPPED)
            {
                this.startSystem();
            }
            sp.WriteLine("logout");
            sp.Close();
        }
                
        #endregion
    }
}
