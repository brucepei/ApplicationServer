using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExpectNet;
using System.Text.RegularExpressions;
using System.Threading;

namespace ApplicationServer
{
    class Program
    {
        static Int32 defaultPort = 16789;
        static Session spawn = null;
        static void Main(string[] args)
        {
            Main_expect(args);
        }

        static String Send(Session sess, String command, Int32 timeout_seconds, String regex_string=null, Boolean needCRLF=true)
        {
            var result = String.Empty;
            timeout_seconds = timeout_seconds * 1000;
            if (needCRLF)
            {
                sess.Send(command + "\n");
            }
            else
            {
                sess.Send(command);
            }
            Regex regex = null;
            if (String.IsNullOrEmpty(regex_string))
            {
                regex = new Regex(@"[a-zA-Z]:[^>\n]*?>");
            }
            else
            {
                regex = new Regex(regex_string);
            }
            try
            {
                sess.Expect(regex, s => { result = s; }, timeout_seconds);
            }
            catch (System.TimeoutException)
            {
                Console.WriteLine("Timeout:" + command);
                if (sess.Process.HasExited)
                {
                    sess.Reset();
                }
                else
                {
                    sess.Send("\n");
                    sess.Send("\n");
                    String buff = sess.ClearBuffer(5000);
                    Console.WriteLine("Clear buffer: " + buff + "!BUFFER!");
                }
            }
            return result;
        }

        static void Main_sock(string[] args)
        {
            Logging.TurnOff = false;
            Logging.Initialize("./as.log");
            Int32 port = 0;
            Boolean useUserPort = false;
            if (args.Length > 0)
            {
                if (Int32.TryParse(args[0], out port))
                {
                    if (port > 0)
                    {
                        useUserPort = true;
                        Logging.WriteLine("Using user port: " + port);
                    }
                }
                if (args.Length > 1)
                {
                    if (args[1] == "-nolog")
                    {
                        Logging.TurnOff = true;
                    }
                }
            }
            if (!useUserPort) 
            {
                port = defaultPort;
                Logging.WriteLine("Using default port: " + port);
            }
            var appServ = new AppServer(port);
            appServ.Start();
        }

        static void Main_expect(string[] args)
        {
            try
            {
                spawn = Expect.Spawn(new ProcessSpawnable("cmd.exe"));
                string banner = spawn.ClearBuffer(1000);
                Console.WriteLine("Cmd started with banner:\n" + banner + "!END!");
                var result = Send(spawn, "ping 127.0.0.1 -n 3", 1);
                Console.WriteLine("result: " + result + "!END!");
                result = Send(spawn, "ping 127.0.0.1 -n 2", 10);
                Console.WriteLine("result: " + result + "!END!");
                result = Send(spawn, "ping 127.0.0.1 -n 2", 1);
                Console.WriteLine("result: " + result + "!END!");
                //spawn.Send("net user\n");
                //spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("net user found:" + s + "!"));
                //spawn.Timeout = 5000;
                //spawn.Send("ping 127.0.0.1 -n 3\n");
                //spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("ping found:" + s + "!"));
                //spawn.Send("cd c:\\Users\n");
                //spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("cd found:" + s + "!"));
                //spawn.Send("dir c:\\\n");
                //spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), (s) => Console.WriteLine("dir c: found: " + s + "!"));
                //spawn.Send("asdsdf\n");
                //spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), (s) => Console.WriteLine("asdsdf found: " + s + "!"));
                //spawn.Send("ping 127.0.0.1 -n 10\n");
                //spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("ping found:" + s + "!"), 15000);
                //spawn.Send("c:\n");

                //spawn.Expect(@">", s => spawn.Send("cd Users\n"));

                //spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), (s) => Console.WriteLine("c: found: " + s + "!"));
                //spawn.Send("whoami\n");
                //spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("whoami found:" + s + "!"));
                //spawn.Send("cd c:\\Users\n");
                //spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("cd found:" + s + "!"));

            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
            Console.ReadKey();
        }
    }
}
