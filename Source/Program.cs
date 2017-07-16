using System;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApplication1
{
    class Program
    {
        public static int TCPPort = 5040;
        static void Main(string[] args)
        {
            //Start a Tas to chek if a key is pressed.
            var tKey = Task.Run(() =>
            {
                Console.WriteLine("Press any key to stop server.");
                Console.ReadLine();
            });

            //Start server in Background
            var tSrv = Task.Run(() => EnecysServer());


            while(!tKey.IsCompleted & !tSrv.IsCompleted)
            {
                Thread.Sleep(500);
            }

            Console.WriteLine("Enecsys Monitor stopped...");
        }

        /// <summary>
        /// Enecsys Server
        /// </summary>
        public static void EnecysServer()
        {
            try
            {
                //Create and start a TcpListener
                TcpListener tServer = new TcpListener(IPAddress.Any, TCPPort);
                tServer.Start();
                Console.WriteLine("Enecsys Monitor started..");

                while (tServer.Server.IsBound)
                {
                    var clientTask = tServer.AcceptTcpClientAsync();
                    if (clientTask.Result != null)
                    {
                        var tcpClient = clientTask.Result;
                        if (tcpClient != null)
                        {
                            while (tcpClient.Connected)
                            {
                                try
                                {
                                    NetworkStream networkStream = tcpClient.GetStream();
                                    if (networkStream.CanRead)
                                    {
                                        // Buffer to store the response bytes.
                                        byte[] readBuffer = new byte[tcpClient.ReceiveBufferSize];

                                        // String that will contain full server reply
                                        StringBuilder fullServerReply = new StringBuilder();

                                        do
                                        {
                                            try
                                            {
                                                int _bytesReaded = networkStream.Read(readBuffer, 0, tcpClient.Available);
                                                string sout = (ASCIIEncoding.ASCII.GetString(readBuffer, 0, _bytesReaded).Replace((char)7, '?'));
                                                if (sout.Length > 18)
                                                {
                                                    string sCode = sout.Substring(18);
                                                    if (sCode.StartsWith("WS="))
                                                    {
                                                        try
                                                        {
                                                            EnecsysData EDs = new EnecsysData(sCode);
                                                            Console.WriteLine("Date:" + DateTime.Now.ToString("HH:mm:ss") + ", Panel:" + EDs.SystemID + ", Power:" + EDs.ACPowerW + "W");
                                                        }
                                                        catch { }
                                                    }
                                                }
                                            }
                                            catch { }
                                        }
                                        while (networkStream.DataAvailable);
                                    }
                                    else
                                    {
                                        Thread.Sleep(10);
                                    }
                                    networkStream.Dispose();
                                }
                                catch { }
                            }

                            tcpClient.Dispose();
                        }
                    }
                    Thread.Sleep(50);
                }

                tServer.Stop();
                Console.WriteLine("An error occured..");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public class EnecsysData
        {
            public double ConvertFromBin64(string base64input)
            {
                double iResult = 0;

                string base64chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

                int i = 0;
                foreach (char a in base64input.ToCharArray())
                {
                    iResult = iResult + base64chars.IndexOf(a) * System.Math.Pow(64, base64input.Length - 1 - i);
                    i++;
                }

                return iResult;
            }

            public EnecsysData(string EncodedString)
            {
                string fixedString = EncodedString.Replace('-', '+');
                fixedString = fixedString.Replace('_', '/');
                byte[] bRes = Convert.FromBase64String(fixedString.Substring(3, 6) + "==");
                SystemID = BitConverter.ToUInt32(bRes, 0);

                double i = ConvertFromBin64(EncodedString.Substring(14, 4));
                TimeSpan iSPan = new TimeSpan(0, 0, (int)i * 30);
                TimeStamp = iSPan;

                ErrorState = EncodedString.Substring(32, 3);
                //DAz = undervoltage

                DCCurrentA = (double)ConvertFromBin64(EncodedString.Substring(35, 2)) / 2000;
                DCPowerW = (double)ConvertFromBin64(EncodedString.Substring(37, 2));
                Efficiency = (double)ConvertFromBin64(EncodedString.Substring(39, 3)) / 40;
                VoltageAC = (double)ConvertFromBin64(EncodedString.Substring(43, 3)) / 4;
                Temperature = (int)ConvertFromBin64(EncodedString.Substring(46, 1));
                CumulativeDCPowerW = (double)ConvertFromBin64(EncodedString.Substring(47, 3)) * 0.25;
                string sACFrequency = EncodedString.Substring(42, 1);
                if (sACFrequency.ToLower() == "x")
                    ACFrequency = 49;
                if (sACFrequency.ToLower() == "y")
                    ACFrequency = 50;

                VoltageDC = DCPowerW / DCCurrentA;
                ACPowerW = DCPowerW * (Efficiency / 100);
                Wh = (double)ConvertFromBin64(EncodedString.Substring(47, 3)) / 4;
                kWh = (double)ConvertFromBin64(EncodedString.Substring(50, 3)) / 16;
                LifekWh = (0.001 * Wh) + kWh;
                /*
                $HexWh = cnv(substr($HexZigbee,66,4),16,10);
                $HexkWh = cnv(substr($HexZigbee,70,4),16,10);
                $LifekWh = (0.001*$HexWh)+$HexkWh; */
            }

            public UInt32 SystemID { get; set; }
            public TimeSpan TimeStamp { get; set; }
            public string ErrorState { get; set; }
            /// <summary>
            /// Current in Ampere
            /// </summary>
            public double DCCurrentA { get; set; }
            public double DCPowerW { get; set; }
            public double ACPowerW { get; set; }
            /// <summary>
            /// Efficiency in %
            /// </summary>
            public double Efficiency { get; set; }
            public double VoltageAC { get; set; }
            public double VoltageDC { get; set; }
            public int Temperature { get; set; }
            /// <summary>
            /// cummulative PowerDC in Wh reset after 1kWh
            /// </summary>
            public double CumulativeDCPowerW { get; set; }
            public int ACFrequency { get; set; }

            double Wh { get; set; }
            double kWh { get; set; }
            double LifekWh { get; set; }


        }
    }
}
