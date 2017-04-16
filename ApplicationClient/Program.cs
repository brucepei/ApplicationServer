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
        const int SendCommand_Timeout = 5000;
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
                var jc = JsonCommand.RunProgram(command, 3000);
                //var jc = JsonCommand.ExpectOutput(command, @"[a-zA-Z]:\\.*?>", 6000);

                var json_result = ConnectAS(ip, port, JSON.Stringify(jc), jc.Timeout + 10000);
                var result = JSON.Parse<JsonCommand>(json_result);
                if (result != null)
                {
                    Console.WriteLine(String.Format("Output: {0}", result.Output));
                    Console.WriteLine(String.Format("Error: {0}", result.Error));
                    Console.WriteLine(String.Format("ExitCode: {0}", result.ExitCode));
                }
            }
            else
            {
                Logging.WriteLine("Wrong arguments! Usage: ac 127.0.0.1:8080 dir!");
            }
        }

        public static IPEndPoint GetEndPoint(string ip, int port)
        {
            IPEndPoint result = null;
            IPAddress ipAddress = null;
            if (!IPAddress.TryParse(ip, out ipAddress))
            {
                IPHostEntry host_entry = null;
                try
                {
                    host_entry = Dns.GetHostEntry(ip);
                }
                catch (Exception ex)
                {
                    Logging.WriteLine("Failed to query IP for hostname {0}: {1}", ip, ex.Message);
                    return result;
                }
                foreach (var addr in host_entry.AddressList)
                {
                    if (addr.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipAddress = addr;
                        break;
                    }
                }
            }
            if (ipAddress == null)
            {
                Logging.WriteLine("Failed to find IPv4 address for: {0}", ip);
                return result;
            }
            
            try
            {
                result = new IPEndPoint(ipAddress, port);
            }
            catch (Exception ex)
            {
                Logging.WriteLine("Failed to combine IP and Port to end point({0}:{1}): {2}", ip, port, ex.Message);
            }
            return result;
        }
        public static Socket ConnectSocket(IPEndPoint remoteEP, int receive_timeout)
        {
            Socket socket = null;
            string remote_addr = remoteEP.ToString();
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, receive_timeout);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, SendCommand_Timeout);
            }
            catch (Exception ex)
            {
                Logging.WriteLine("Failed to configure socket({0}): {1}", remote_addr, ex.Message);
                return null;
            }

            try
            {
                socket.Connect(remoteEP);
            }
            catch (Exception ex)
            {
                Logging.WriteLine("Failed to connect to socket({0}): {1}", remote_addr, ex.Message);
                return null;
            }
            Logging.WriteLine("Socket connected to {0}", socket.RemoteEndPoint.ToString());
            return socket;
        }

        public static string ConnectAS(string asIP, int asPort, string cmd, int timeout)
        {
            string result = String.Empty;
            // Connect to a remote device
            var remoteEP = GetEndPoint(asIP, asPort);
            Socket sender = ConnectSocket(remoteEP, timeout);
            if (sender == null)
                return result;
            var send_done = false; //even send failed, it need close socket, cannot return directly!
            try
            {
                byte[] msg = Encoding.ASCII.GetBytes(String.Format("{0}\n", cmd));
                int bytesSent = sender.Send(msg);
                Logging.WriteLine(@"Send msg+\n({0} bytes): <#{1}#>", bytesSent, cmd);
                send_done = true;
            }
            catch (Exception ex)
            {
                Logging.WriteLine("Failed to send cmd {0}: {1}", cmd, ex.Message);
            }
            if (send_done)
            {
                try
                {
                    byte[] bytes = new byte[1024];
                    while (true)
                    {
                        int bytesRec = sender.Receive(bytes);
                        result += Encoding.UTF8.GetString(bytes, 0, bytesRec);
                        if (bytesRec == 0)
                        {
                            Logging.WriteLine("Read 0 bytes, so no bytes are avaiable!");
                            break;
                        }
                    }
                    Logging.WriteLine("Got AS response: <#{0}#>", result);
                }
                catch (Exception ex)
                {
                    Logging.WriteLine("Receiving data with exception: {0}", ex.Message);
                }
            }
            sender.Shutdown(SocketShutdown.Both);
            sender.Close();
            return result;
        }
    }
}
