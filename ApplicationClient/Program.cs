using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using ApplicationServer;
using System.Runtime.Serialization;
using CommandProtocol;
using System.Runtime.Serialization.Json;
using System.IO;

namespace ApplicationClient
{
    class Program
    {
        const int Default_AS_Port = 8080;
        private static byte[] result = new byte[1024];
        static void Main(string[] args)
        {
            Logging.TurnOff = false;
            Logging.Initialize("ac.log");

            int port = Default_AS_Port;
            if (args.Length >= 2)
            {
                var ip_port_tuple = args[0].Split(new char[] { ':' }, 2);
                var ip = ip_port_tuple[0];
                if (ip_port_tuple.Length > 1)
                {
                    if (Int32.TryParse(ip_port_tuple[1], out port))
                    {
                        Logging.WriteLine("Cannot parse port string {0}", ip_port_tuple[1]);
                    }
                }
                //var jc = new ExpectOutputCommand("dir c:", @"[a-zA-Z]:\.*?>", 5000);
                var command = String.Join(" ", args, 1, args.Length - 1);
                var jc = new RunProgramCommand(command, 60000);
                ConnectAS(ip, port, jc.ToJson());
            }
            else
            {
                Logging.WriteLine("Wrong arguments! Usage: ac 127.0.0.1:8080 dir!");
            }
        }

        public static string ConnectAS(string asIP, int asPort, string cmd)
        {
            String result = String.Empty;
            byte[] bytes = new byte[8192];
            // Connect to a remote device
            IPAddress ipAddress = null;

            foreach (var addr in Dns.GetHostEntry(asIP).AddressList)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    ipAddress = addr;
                    break;
                }
            }
            if (ipAddress == null)
            {
                Logging.WriteLine("Cannot resolve ip: {0}", asIP);
                return result;
            }
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, asPort);
            // Create a TCP/IP  socket.  
            Socket sender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sender.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 60000);
            sender.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 60000);
            // Connect the socket to the remote endpoint. Catch any errors.  
            sender.Connect(remoteEP);
            Logging.WriteLine("Socket connected to {0}", sender.RemoteEndPoint.ToString());

            // Encode the data string into a byte array.  
            byte[] msg = Encoding.ASCII.GetBytes(String.Format("{0}\n", cmd));
            // Send the data through the socket.
            Logging.WriteLine("Send msg: <#{0}#>", cmd);
            int bytesSent = sender.Send(msg);
            sender.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 10000);
            // Receive the response from the remote device.  
            int bytesRec = sender.Receive(bytes);
            result = Encoding.UTF8.GetString(bytes, 0, bytesRec);
            Logging.WriteLine("Got AS response: <#{0}#>", result);

            // Release the socket.  
            sender.Shutdown(SocketShutdown.Both);
            sender.Close();

            return result;
        }
    }
}
