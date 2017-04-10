﻿using System;
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
        static void Main(string[] args)
        {
            Main_expect(args);
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
                Console.WriteLine("ExampleApp");
                Session spawn = Expect.Spawn(new ProcessSpawnable("cmd.exe"));
                spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("cmd: got: " + s + "!"));
                spawn.Send("net user\n");
                spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("net user found:" + s + "!"));
                spawn.Timeout = 5000;
                spawn.Send("ping 127.0.0.1 -n 3\n");
                spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("ping found:" + s + "!"));
                spawn.Send("cd c:\\Users\n");
                spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("cd found:" + s + "!"));
                spawn.Send("dir c:\\\n");
                spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), (s) => Console.WriteLine("dir c: found: " + s + "!"));
                spawn.Send("asdsdf\n");
                spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), (s) => Console.WriteLine("asdsdf found: " + s + "!"));
                spawn.Send("ping 127.0.0.1 -n 10\n");
                spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("ping found:" + s + "!"), 15000);
                spawn.Send("c:\n");
                //spawn.Expect(@">", s => spawn.Send("cd Users\n"));
                spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), (s) => Console.WriteLine("c: found: " + s + "!"));
                spawn.Send("whoami\n");
                spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("whoami found:" + s + "!"));
                spawn.Send("cd c:\\Users\n");
                spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("cd found:" + s + "!"));

                // Expect timeouts examples
                spawn.Send("ping 8.8.8.8\n");
                try
                {
                    //spawn.Expect("8.8.8.8 的 Ping 统计信息", s => Console.WriteLine(s), 6000);
                    spawn.Expect("Ping statistics", s => Console.WriteLine(s));
                }
                catch (System.TimeoutException)
                {
                    Console.WriteLine("Timeout 8.8.8.8!");
                }
                spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("clear buffer1 found:" + s + "!"));

                spawn.Send("ping 8.8.4.4\n");
                try
                {
                    //spawn.Expect("8.8.4.4 的 Ping 统计信息", s => Console.WriteLine(s), 6000);
                    spawn.Expect("Ping statistics for 8.8.4.4", s => Console.WriteLine(s));
                }
                catch (System.TimeoutException)
                {
                    Console.WriteLine("Timeout 8.8.4.4!");
                }
                spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("clear buffer2 found:" + s + "!"), 15000);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
            Console.ReadKey();
        }
    }
}
