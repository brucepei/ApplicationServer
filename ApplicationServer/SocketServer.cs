using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using System.Threading.Tasks;
using CommandProtocol;

namespace ApplicationServer
{
    public struct Client
    {
        public Client(String addr, Socket sock, Task<CommandResult> task)
        {
            Address = addr;
            Socket = sock;
            Task = task;
        }
        public String Address;
        public Socket Socket;
        public Task<CommandResult> Task;
    }

    public class AppServer
    {
        public AppServer(Int32 port)
        {
            listenPort = port;
            clients = new Dictionary<string, Client>();
        }
        private Int32 listenPort;
        public Int32 ListenPort
        {
            get { return listenPort; }
        }
        Dictionary<string, Client> clients;
        public void Start()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var address = IPAddress.Any;
            var ip_port_entry = new IPEndPoint(address, listenPort);
            try
            {
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, 128);
                socket.Bind(ip_port_entry);
            }
            catch (SocketException se)
            {
                Logging.WriteLine("Socket exception:" + se.Message);
            }
            catch (Exception ex)
            {
                Logging.WriteLine("Unexpected exception when bind socket:" + ex.Message);
            }
            socket.Listen(10000);
            new Task(() => CheckClients()).Start();
            Logging.WriteLine("Waiting for connection at port:" + listenPort);
            Logging.WriteLine("Get Socket server ttl = {0}", socket.Ttl);
            while (true)
            {
                Socket clientSocket = socket.Accept();
                var client_ip_port = clientSocket.RemoteEndPoint.ToString();
                Logging.WriteLine("Client connected:" + client_ip_port);
                var task = new Task<CommandResult>(sock=>ReceiveCommand((Socket)sock), clientSocket);
                task.Start();
                if (clients.ContainsKey(client_ip_port))
                {
                    clients[client_ip_port] = new Client(client_ip_port, clientSocket, task);
                }
                else
                {
                    clients.Add(client_ip_port, new Client(client_ip_port, clientSocket, task));
                }
            }
        }

        public void CheckClients()
        {
            while (true)
            {
                System.Threading.Thread.Sleep(1000);
                var doneClients = new List<Client>();
                if (clients.Count > 0)
                {
                    Logging.WriteLine("Check client: active=" + clients.Count);
                }
                foreach (var client in clients.Values)
                {
                    if (client.Task.IsCompleted)
                    {
                        try
                        {
                            Logging.WriteLine(String.Format("check Client {0} done: stdout: {1}, stderr: {2}!", client.Address, client.Task.Result.Output, client.Task.Result.Error));
                        }
                        catch (Exception ex)
                        {
                            Logging.WriteLine(String.Format("check Client {0} error: Unexpected exception when check client: {1}", client.Address, ex.Message));
                        }
                        doneClients.Add(client);
                    }
                    //else
                    //{
                    //    Logging.WriteLine(String.Format("Client {0} is still alive", client.Address));
                    //}
                }
                foreach (var c in doneClients)
                {
                    clients.Remove(c.Address);
                }
            }
        }

        public CommandResult ReceiveCommand(Socket sock)
        {
            CommandResult cmdResult = new CommandResult(null, null, -1);
            byte[] buf = new byte[4096];
            int length = -1;
            var remoteAddr = sock.RemoteEndPoint.ToString();
            try
            {
                sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 10000); //timeout if client idle more than...
                length = sock.Receive(buf);
                if (length > 0)
                {
                    var cmd = Encoding.ASCII.GetString(buf, 0, length);
                    Logging.WriteLine(remoteAddr + ": Received:" + cmd + "!!!");
                    var result = String.Empty;
                    JsonCommand cmd_rc = JSON.Parse<JsonCommand>(cmd);
                    if (cmd_rc != null)
                    {
                        if (cmd_rc.Type == CommandType.RunProgram)
                        {
                            try
                            {
                                var app = new Application(cmd_rc.Command);
                                cmdResult = app.Run(cmd_rc.Timeout);
                                cmd_rc.Output = cmdResult.Output;
                                cmd_rc.Error = cmdResult.Error;
                                cmd_rc.ExitCode = cmdResult.ExitCode;
                                result = JSON.Stringify(cmd_rc);
                            }
                            catch (Exception ex)
                            {
                                Logging.WriteLine("Run RunProgram {0} with exception: {1}", cmd_rc.Command, ex.Message);
                                cmd_rc.Exception = ex.Message;
                            }
                        }
                        else if (cmd_rc.Type == CommandType.ExpectOutput)
                        {
                            try
                            {
                                var expect_type = (int)cmd_rc.ExpectType;
                                Logging.WriteLine("Run ExpectOutput with type: {0}", expect_type);
                                cmd_rc.Output = Program.Sessions[expect_type].Cmd(cmd_rc.Command, cmd_rc.Timeout, cmd_rc.RegexString);
                            }
                            catch (Exception ex)
                            {
                                Logging.WriteLine("Run ExpectOutput {0} with exception: {1}", cmd_rc.Command, ex.Message);
                                cmd_rc.Exception = ex.Message;
                            }
                            result = JSON.Stringify(cmd_rc);
                        }
                        else if (cmd_rc.Type == CommandType.ClearExpectBuffer)
                        {
                            try
                            {
                                var expect_type = (int)cmd_rc.ExpectType;
                                Logging.WriteLine("Run ClearExpectBuffer with type: {0}", expect_type);
                                cmd_rc.Output = Program.Sessions[expect_type].ClearBuffer(cmd_rc.Timeout);
                            }
                            catch (Exception ex)
                            {
                                Logging.WriteLine("Run ClearExpectBuffer {0} with exception: {1}", cmd_rc.Command, ex.Message);
                                cmd_rc.Exception = ex.Message;
                            }
                            result = JSON.Stringify(cmd_rc);
                        }
                        else if (cmd_rc.Type == CommandType.ResetExpectSession)
                        {
                            try
                            {
                                var expect_type = (int)cmd_rc.ExpectType;
                                Logging.WriteLine("Run ResetExpectSession with type: {0}", expect_type);
                                if (Program.Sessions[expect_type].Reset())
                                {
                                    cmd_rc.Output = Program.Sessions[expect_type].ClearBuffer(cmd_rc.Timeout);
                                }
                                else
                                {
                                    cmd_rc.Exception = "Failed to reset process with unknown reason";
                                }
                            }
                            catch (Exception ex)
                            {
                                Logging.WriteLine("Run ClearExpectBuffer {0} with exception: {1}", cmd_rc.Command, ex.Message);
                                cmd_rc.Exception = ex.Message;
                            }
                            result = JSON.Stringify(cmd_rc);
                        }
                    }
                    else
                    {
                        var command = cmd.Trim().Trim('"').Trim();
                        var regex = new System.Text.RegularExpressions.Regex(@"ia:(\d*):(.*?):(.+)");
                        var match = regex.Match(command);
                        if (match.Success)
                        {
                            int timeout = 0;
                            Int32.TryParse(match.Groups[1].Value, out timeout);
                            var expect_str = match.Groups[2].Value;
                            command = match.Groups[3].Value;
                            result = Program.Sessions[0].Cmd(command, timeout, expect_str);
                        }
                        else
                        {
                            var app = new Application(cmd);
                            cmdResult = app.Run(60);
                            result = String.Format("cmd {0} done with stdout: '{1}', stderr: '{2}'!\n", cmd, cmdResult.Output, cmdResult.Error);
                            //Logging.WriteLine(result);
                        }
                    }
                    byte[] msg = Encoding.UTF8.GetBytes(result);
                    sock.Send(msg);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteLine(remoteAddr + ": Unexpected exception when receive command:" + ex.Message);
            }
            sock.Shutdown(SocketShutdown.Both);
            sock.Close();
            Logging.WriteLine(remoteAddr + ": Receive command done!");
            return cmdResult;
        }
    }
}
