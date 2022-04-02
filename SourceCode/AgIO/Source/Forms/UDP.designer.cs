﻿using System;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace AgIO
{
    public class CTraffic
    {
        public int cntrPGNFromAOG = 0;
        public int cntrPGNToAOG = 0;
     
        //public int cntrUDPOut = 0;
        //public int cntrUDPIn = 0;

        public int cntrGPSOut = 0;
        public int cntrGPSIn = 0;

        public int cntrGPS2In = 0;
        public int cntrGPS2Out = 0;

        public int cntrSteerIn = 0;
        public int cntrSteerOut = 0;

        public int cntrMachineIn = 0;
        public int cntrMachineOut = 0;

        public int cntrModule3In = 0;
        public int cntrModule3Out = 0;

        public bool isTrafficOn = true;

        public uint helloFromMachine = 0, helloFromAutoSteer = 0;
    }

    public partial class FormLoop
    {
        // loopback sockets
        private Socket recvFromAOGLoopBackSocket;
        private Socket sendToAOGLoopBackSocket;
        private Socket sendToAgVRLoopBackSocket;

        // Send and Recv socket for udp network
        private Socket sendToUDPSocket;
        private Socket recvFromUDPSocket;
        private bool isUDPNetworkConnected;

        //2 endpoints for local and udp
        IPEndPoint epAgOpen;
        IPEndPoint epModule;
        IPEndPoint epAgVR;

        //class for counting bytes
        public CTraffic traffic = new CTraffic();

        // Data stream
        private byte[] buffer = new byte[1024];

        //IP address and port of Auto Steer server
        IPAddress epIP = IPAddress.Parse(Properties.Settings.Default.setIP_autoSteerIP);

        //initialize loopback and udp network
        private void LoadUDPNetwork()
        {
            try //udp network
            {
                // Initialise the socket
                sendToUDPSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                
                IPAddress localIPAddress = IPAddress.Parse(Properties.Settings.Default.setIP_localAOG);
                IPEndPoint localEndPoint = new IPEndPoint(localIPAddress, 0);
                sendToUDPSocket.Bind(localEndPoint);

                sendToUDPSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);

                // AgIO sends to this endpoint - usually 192.168.5.255:8888
                epModule = new IPEndPoint(epIP, 8888);

                //Initialize Recv socket
                recvFromUDPSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                recvFromUDPSocket.EnableBroadcast = true;

                // AgIO listens to this endpoint
                recvFromUDPSocket.Bind(new IPEndPoint(IPAddress.Any, 9999));

                // Initialise the IPEndPoint for async listener!
                EndPoint client = new IPEndPoint(IPAddress.Any, 0);

                // Start listening for incoming data
                recvFromUDPSocket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref client, 
                    new AsyncCallback(ReceiveDataUDPAsync), recvFromUDPSocket);
                isUDPNetworkConnected = true;
                btnUDP.BackColor = Color.LightGreen;
            }
            catch (Exception e)
            {
                //WriteErrorLog("UDP Server" + e);
                MessageBox.Show("Network Address -> " + Properties.Settings.Default.setIP_localAOG + " May not exist. \r\n" 
                    + "Are you sure ethernet is connected?\r\n\r\n" 
                    + "Windows Error Message: " + e.Message, "Network Connection Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnUDP.BackColor = Color.Orange;
            }
        }

        private void LoadLoopback()
        { 
            try //loopback
            {
                // Initialise the socket
                sendToAOGLoopBackSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                sendToAOGLoopBackSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                sendToAOGLoopBackSocket.Bind(new IPEndPoint(IPAddress.Loopback, 17770));

                //AgIO sends to AgOpen on this endpoint
                epAgOpen = new IPEndPoint(IPAddress.Parse("127.255.255.255"), 15555);
               
                // Initialise the AgVR send socket
                sendToAgVRLoopBackSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                //sendToLoopBackSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                sendToAgVRLoopBackSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                sendToAgVRLoopBackSocket.Bind(new IPEndPoint(IPAddress.Loopback, 17776));

                //AgIO sends to AgVR on this endpoint
                epAgVR = new IPEndPoint(IPAddress.Parse("127.255.255.255"), 16666);
                
                // Initialise the socket
                recvFromAOGLoopBackSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                // AgIO listens on this port
                recvFromAOGLoopBackSocket.Bind(new IPEndPoint(IPAddress.Any, 17777));

                // Initialise the IPEndPoint for the client
                EndPoint client = new IPEndPoint(IPAddress.Any, 0);

                // Start listening for incoming data
                recvFromAOGLoopBackSocket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref client, 
                    new AsyncCallback(ReceiveDataLoopAsync), recvFromAOGLoopBackSocket);
            }
            catch (Exception ex)
            {
                //lblStatus.Text = "Error";
                MessageBox.Show("Load Error: " + ex.Message, "UDP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //loopback functions
        #region Send And Receive

        public byte[] nmeaHeader = new byte[] { 0x80, 0x81, 0x7F, 0xCF };

        //local back to AOG - packaged into pgn
        private void SendToLoopBackMessageAOG(string message)
        {
            try
            {
                // Get packet as byte array
                byte[] byteData = Encoding.ASCII.GetBytes(message);

                //combine into header and nmea char string now bytes
                byte[] nmeaData = glm.Combine(nmeaHeader, byteData);

                if (nmeaData.Length != 0)
                {
                    traffic.cntrPGNToAOG += nmeaData.Length;

                    // Send packet to the zero
                    sendToAOGLoopBackSocket.BeginSendTo(nmeaData, 0, nmeaData.Length, SocketFlags.None, epAgOpen,
                            new AsyncCallback(SendDataLoopAsync), null);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Send Error: " + ex.Message, "UDP Client", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SendToLoopBackMessageAOG(byte[] byteData)
        {
            try
            {
                if (byteData.Length != 0)
                {
                    traffic.cntrPGNToAOG += byteData.Length;

                    // Send packet to AOG
                    sendToAOGLoopBackSocket.BeginSendTo(byteData, 0, byteData.Length, SocketFlags.None, epAgOpen,
                        new AsyncCallback(SendDataLoopAsync), null);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Send Error: " + ex.Message, "UDP Client", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SendToLoopBackMessageVR(byte[] byteData)
        {
            try
            {
                if (byteData.Length != 0)
                {
                    //traffic.cntrPGNToAOG += byteData.Length;

                    // Send packet to AgVR
                    sendToAgVRLoopBackSocket.BeginSendTo(byteData, 0, byteData.Length, SocketFlags.None, epAgVR,
                        new AsyncCallback(SendDataVRLoopAsync), null);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Send Error: " + ex.Message, "UDP Client", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ReceiveFromLoopBack(int port, byte[] data)
        {
            //coming from AOG
            if (port == 15550)
            {
                traffic.cntrPGNFromAOG += data.Length;

                //Send out to udp network
                if (isUDPNetworkConnected)
                {
                    SendUDPMessage(data);
                }

                //send out to VR Loopback
                if (isPluginUsed) SendToLoopBackMessageVR(data);
            }

            //coming from VR plugin
            else if (port == 16660)
            {
                SendToLoopBackMessageAOG(data);
            }
        }

        public void SendDataLoopAsync(IAsyncResult asyncResult)
        {
            try
            {
                sendToAOGLoopBackSocket.EndSend(asyncResult);
            }
            catch (Exception ex)
            {
                MessageBox.Show("SendData Error: " + ex.Message, "UDP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        public void SendDataVRLoopAsync(IAsyncResult asyncResult)
        {
            try
            {
                sendToAgVRLoopBackSocket.EndSend(asyncResult);
            }
            catch (Exception ex)
            {
                MessageBox.Show("SendData Error: " + ex.Message, "UDP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void ReceiveDataLoopAsync(IAsyncResult asyncResult)
        {
            try
            {
                // Initialise the IPEndPoint for the clients
                EndPoint epSender = new IPEndPoint(IPAddress.Any, 0);

                // Receive all data
                int msgLen = recvFromAOGLoopBackSocket.EndReceiveFrom(asyncResult, ref epSender);

                byte[] localMsg = new byte[msgLen];
                Array.Copy(buffer, localMsg, msgLen);

                // Listen for more connections again...
                recvFromAOGLoopBackSocket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref epSender, 
                    new AsyncCallback(ReceiveDataLoopAsync), recvFromAOGLoopBackSocket);

                // Update status through a delegate
                int port = ((IPEndPoint)epSender).Port;

                //BeginInvoke(updateStatusDelegateLoopback, new object[] { port, localMsg });
                BeginInvoke((MethodInvoker)(() => ReceiveFromLoopBack(port, localMsg)));
            }
            catch (Exception)
            {
                //MessageBox.Show("ReceiveData Error: " + ex.Message, "UDP Server", MessageBoxButtons.OK,
                //MessageBoxIcon.Error);
            }
        }

        #endregion

        //udp network functions
        public void SendUDPMessage(string message)
        {
            if (isUDPNetworkConnected)
            {
                try
                {
                    // Get packet as byte array to send
                    byte[] byteData = Encoding.ASCII.GetBytes(message);
                    if (byteData.Length != 0)
                        sendToUDPSocket.BeginSendTo(byteData, 0, byteData.Length, SocketFlags.None,
                            epModule, new AsyncCallback(SendDataUDPAsync), null);

                    //traffic.cntrUDPOut+=byteData.Length;
                }
                catch (Exception)
                {
                    //WriteErrorLog("Sending UDP Message" + e.ToString());
                    //MessageBox.Show("Send Error: " + e.Message, "UDP Client", MessageBoxButtons.OK,
                    //MessageBoxIcon.Error);
                }
            }
        }

        //sends byte array
        public void SendUDPMessage(byte[] byteData)
        {
            if (isUDPNetworkConnected)
            {
                try
                {
                    IPEndPoint epAutoSteer = new IPEndPoint(epIP, 8888);

                    // Send packet to the zero
                    if (byteData.Length != 0)
                    {
                        sendToUDPSocket.BeginSendTo(byteData, 0, byteData.Length, SocketFlags.None,
                            epModule, new AsyncCallback(SendDataUDPAsync), null);      
                    }

                    if (byteData[3] == 254) traffic.cntrSteerIn += byteData.Length;
                    if (byteData[3] == 239) traffic.cntrMachineIn += byteData.Length;

                    //traffic.cntrUDPOut+=byteData.Length;

                    //if (byteData[0] == 0x80 && byteData[1] == 0x81)
                    //{
                    //    //module return via udp sent to AOG
                    //    SendToLoopBackMessageAOG(byteData);

                    //    //module byteData also sent to VR
                    //    SendToLoopBackMessageVR(byteData);

                    //}
                }
                catch (Exception)
                {
                    //WriteErrorLog("Sending UDP Message" + e.ToString());
                    //MessageBox.Show("Send Error: " + e.Message, "UDP Client", MessageBoxButtons.OK,
                    //MessageBoxIcon.Error);
                }
            }
        }

        private void ReceiveFromUDP(int port, byte[] data)
        {
            if (port != 10000)
            {
                if (data[0] == 0x80 && data[1] == 0x81)
                {
                    //module return via udp sent to AOG
                    SendToLoopBackMessageAOG(data);

                    //module data also sent to VR
                    if (isPluginUsed) SendToLoopBackMessageVR(data);

                    if (data[3] == 253) traffic.cntrSteerOut += data.Length;
                    if (data[3] == 199) traffic.cntrSteerOut += data.Length;
                    if (data[3] == 237) 
                        traffic.cntrMachineOut += data.Length;

                    //reset hello counters
                    if (data[3] == 197) traffic.helloFromMachine = 0;
                    if (data[3] == 199) traffic.helloFromAutoSteer = 0;
                }
                //$ = 36 G=71 P=80 K=75
                else if (data[0] == 36 && (data[1] == 71 || data[1] == 80 || data[1] == 75))
                {
                    //if (timerSim.Enabled) DisableSim();
                    traffic.cntrGPSOut += data.Length;
                    rawBuffer += Encoding.ASCII.GetString(data);
                    ParseNMEA(ref rawBuffer);
                }
            }
            
            else //is tool NMEA set to port 10,000
            {
                if (data[0] == 0x80 && data[1] == 0x81)
                {
                    //module return via udp sent to AOG
                    SendToLoopBackMessageAOG(data);

                    return;

                    //module data also sent to VR
                    //SendToLoopBackMessageVR(data);
                }

                //GGA or Panda or PAOGI
                if (data[0] == 36 && (data[1] == 71 || data[1] == 80))
                {
                    traffic.cntrGPS2In += data.Length;
                    rawBuffer2 += Encoding.ASCII.GetString(data);
                    ParseNMEA2(ref rawBuffer2);
                }
            }

            //traffic.cntrUDPIn += data.Length;
        }

        public void SendUDPMessageNTRIP(byte[] byteData, int port)
        {
            if (isUDPNetworkConnected)
            {
                try
                {
                    IPEndPoint epAutoSteer = new IPEndPoint(epIP, port);

                    // Send packet to the zero
                    if (byteData.Length != 0)
                        sendToUDPSocket.BeginSendTo(byteData, 0, byteData.Length, SocketFlags.None, epAutoSteer, 
                            new AsyncCallback(SendDataUDPAsync), null);

                    //traffic.cntrUDPOut += byteData.Length;

                    if (traffic.cntrGPSIn > 4000) traffic.cntrGPSIn = 0;
                    traffic.cntrGPSIn += byteData.Length;

                }
                catch (Exception)
                {
                    //WriteErrorLog("Sending UDP Message" + e.ToString());
                    //MessageBox.Show("Send Error: " + e.Message, "UDP Client", MessageBoxButtons.OK,
                    //MessageBoxIcon.Error);
                }
            }
        }

        private void SendDataUDPAsync(IAsyncResult asyncResult)
        {
            try
            {
                sendToUDPSocket.EndSend(asyncResult);
            }
            catch (Exception)
            {
                //WriteErrorLog(" UDP Send Data" + e.ToString());
                //MessageBox.Show("SendData Error: " + e.Message, "UDP Server", MessageBoxButtons.OK,
                //MessageBoxIcon.Error);
            }
        }

        private void ReceiveDataUDPAsync(IAsyncResult asyncResult)
        {
            try
            {
                // Initialise the IPEndPoint for the client
                EndPoint epSender = new IPEndPoint(IPAddress.Any, 0);

                // Receive all data
                int msgLen = recvFromUDPSocket.EndReceiveFrom(asyncResult, ref epSender);

                byte[] localMsg = new byte[msgLen];
                Array.Copy(buffer, localMsg, msgLen);

                // Listen for more connections again...
                recvFromUDPSocket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref epSender, 
                    new AsyncCallback(ReceiveDataUDPAsync), recvFromUDPSocket);

                int port = ((IPEndPoint)epSender).Port;
                BeginInvoke((MethodInvoker)(() => ReceiveFromUDP(port, localMsg)));

            }
            catch (Exception)
            {
                //WriteErrorLog("UDP Recv data " + e.ToString());
                //MessageBox.Show("ReceiveData Error: " + e.Message, "UDP Server", MessageBoxButtons.OK,
                //MessageBoxIcon.Error);
            }
        }
    }
}
