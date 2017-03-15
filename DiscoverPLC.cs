using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PLCLib
{
    /// <summary>
    /// Finds WinPLCs on network using UDP.
    /// </summary>
    class DiscoverPLC
    {
        // IP and Port of this computer
        IPEndPoint thisEndPoint;

        List<FoundPLC> PLCs = new List<FoundPLC>();
        Dictionary<string, FoundPLC> getPLCByName = new Dictionary<string, FoundPLC>();

        // increment every send UDP packet
        byte sendIncr = 1;
        byte lastIncr = 1;

        // PLCs use this port for UDP
        const int plcUDPPort = 28784;

        public FoundPLC getPLC(string nameOrIP)
        {
            FoundPLC plc;
            getPLCByName.TryGetValue(nameOrIP, out plc);
            return plc;
        }

        /// <summary>
        /// Populate list PLCs.
        /// </summary>
        public int Discover()
        {
            broadcast();
				Thread.Sleep(100);
            if (createPLCList() > 0)
            {
                confirmPLCs();
            }
            return PLCs.Count;
        }

        /// <summary>
        /// Initial broadcast, all WinPLCs respond.
        /// </summary>
        private void broadcast()
        {
            UdpClient udp = new UdpClient();
            udp.Client.ReceiveTimeout = 200;
            udp.Client.SendTimeout = 200;

            udp.Connect("255.255.255.255", plcUDPPort);
            thisEndPoint = (IPEndPoint)udp.Client.LocalEndPoint;
            udp.EnableBroadcast = true;
            // All WinPLCs return UDP packet
            byte[] sendBuf = {0x48, 0x41, 0x50, sendIncr, 0x01, 0xa5, 0x50, 0x01, 0x00, 0x05};
            udp.Send(sendBuf, sendBuf.Length);
            lastIncr = sendIncr++;
            udp.EnableBroadcast = false;
            udp.Close();
        }

        /// <summary>
        /// Create new FoundPLC for every response.
        /// </summary>
        private int createPLCList()
        {
            PLCs.Clear();
            UdpClient udp = new UdpClient(thisEndPoint.Port);
            udp.Client.ReceiveTimeout = 500;
            udp.Client.SendTimeout = 500;
            IPEndPoint plcEndPoint = new IPEndPoint(IPAddress.Any, thisEndPoint.Port);
            do
            {
                try
                {
                    byte[] inBuf = udp.Receive(ref plcEndPoint);
                    byte[] check = { 0x48, 0x41, 0x50, lastIncr };
                    int dataIndex = 0;
                    if (checkData(inBuf, check, dataIndex))
                    {
                        FoundPLC plc = new FoundPLC();
                        plc.ipEndPoint = plcEndPoint;
                        int macIndex = 11;
                        Array.Copy(inBuf, macIndex, plc.MacAddress, 0, plc.MacAddress.Length);
                        PLCs.Add(plc);
                    }
                }
                catch (Exception)
                {
                    break;
                }
                if (udp.Available == 0)Thread.Sleep(300);
            } while (udp.Available > 0);
            udp.Close();
            return PLCs.Count;
        }

        /// <summary>
        /// Confirms PLCs and gets info.
        /// </summary>
        private void confirmPLCs()
        {
            List<FoundPLC> results = new List<FoundPLC>();
            foreach (FoundPLC plc in PLCs)
            {
                if (confirmPLC(plc))
                {
                    results.Add(plc);
                }
            }
            PLCs = results;

            results = new List<FoundPLC>();
            getPLCByName.Clear();
            foreach (FoundPLC plc in PLCs)
            {
                if (confirmPLC(plc))
                {
                    if (getPLCName(plc))
                    {
                        if (getPLCDescription(plc))
                        {
                            results.Add(plc);
                            getPLCByName.Add(plc.Name, plc);
                            getPLCByName.Add(plc.ipEndPoint.Address.ToString(), plc);
                            Console.WriteLine("");
                            Console.WriteLine(plc.Name);
                            Console.WriteLine(plc.ipEndPoint.Address.ToString());
                            Console.WriteLine(BitConverter.ToString(plc.MacAddress));
                            Console.WriteLine(plc.Description);
                            Console.WriteLine("");
                        }
                    }
                }
            }
            PLCs = results;
        }

        private bool confirmPLC(FoundPLC plc)
        {
            byte[] sendBuf = { 0x48, 0x41, 0x50, sendIncr, 0x00, 0x84, 0x40, 0x01, 0x00, 0x04 };
            byte[] check = { 0x48, 0x41, 0x50, sendIncr };

            byte[] data = sendReceive(plc, sendBuf);

            int dataIndex = 0;
            int macIndex = 13;
            return checkData(data, check, dataIndex) && checkData(data, plc.MacAddress, macIndex);
        }

        private bool getPLCName(FoundPLC plc)
        {
            byte[] sendBuf = { 0x48, 0x41, 0x50, sendIncr, 0x00, 0xca, 0xb7, 0x04, 0x00, 0x0b, 0x00, 0x16, 0x00 };
            byte[] check = { 0x48, 0x41, 0x50, sendIncr };

            byte[] data = sendReceive(plc, sendBuf);

            if (!checkData(data, check, 0)) return false;

            int nameIndex = 11;
            int lastChar;
            for (lastChar = nameIndex; lastChar < 255; lastChar++)
            {
                if (data[lastChar] == 0) break;
            }

            byte[] name = new byte[lastChar - nameIndex];
            Array.Copy(data, nameIndex, name, 0, lastChar - nameIndex);

            plc.Name = Encoding.UTF8.GetString(name);

            return true;
        }

        private bool getPLCDescription(FoundPLC plc)
        {
            byte[] sendBuf = { 0x48, 0x41, 0x50, sendIncr, 0x00, 0x5f, 0xb2, 0x04, 0x00, 0x0b, 0x00, 0x26, 0x00 };
            byte[] check = { 0x48, 0x41, 0x50, sendIncr };

            byte[] data = sendReceive(plc, sendBuf);

            if (!checkData(data, check, 0)) return false;

            int lastChar;
            for (lastChar = data.Length; lastChar > 0; lastChar--)
            {
                if (data[lastChar - 1] != 0) break;
            }

            int descriptionIndex = 11;
            byte[] description = new byte[lastChar - descriptionIndex];
            Array.Copy(data, descriptionIndex, description, 0, lastChar - descriptionIndex);

            plc.Description = Encoding.UTF8.GetString(description);
            return true;
        }

        /// <summary>
        /// Sends buffer to PLC, returns response.
        /// Returns empty buffer on fail.
        /// </summary>
        private byte[] sendReceive(FoundPLC plc, byte[] sendBuf, int retries = 0)
        {
            UdpClient udp = new UdpClient(plc.ipEndPoint.Address.ToString(), plcUDPPort);
            IPEndPoint ep = new IPEndPoint(plc.ipEndPoint.Address, plcUDPPort);
            udp.Client.SendTimeout = 1000;
            udp.Client.ReceiveTimeout = 1000;

            // If sending fails, send again up to 3 times.
            try
            {
                udp.Send(sendBuf, sendBuf.Length);
                lastIncr = sendIncr++;
            }
            catch (Exception)
            {
                udp.Close();
                if (retries < 3)
                    return sendReceive(plc, sendBuf, ++retries);
                else
                {
                    byte[] fail = new byte[10];
                    return fail;
                }
            }

            // If read fails, try to read again.
            // If second read fails, try sendRecieve again, up to 3 times.
            try
            {
                byte[] inBuf = udp.Receive(ref ep);
                udp.Close();
                return inBuf;
            }
            catch (Exception)
            {
                try
                {
                    byte[] inBuf = udp.Receive(ref ep);
                    udp.Close();
                    return inBuf;
                }
                catch (Exception)
                {
                    udp.Close();
                    Console.WriteLine("readfail");
                    if (retries < 3)
                        return sendReceive(plc, sendBuf, ++retries);
                    else
                    {
                        byte[] fail = new byte[10];
                        return fail;
                    }
                }
            }
        }

        /// <summary>
        /// Checks to see if bytes in data and check match.
        /// </summary>
        private bool checkData(byte[] data, byte[] check, int dataIndex)
        {
            byte[] testArray = new byte[check.Length];
            Array.Copy(data, dataIndex, testArray, 0, testArray.Length);
            return Enumerable.SequenceEqual(check, testArray);
        }

    }
}
