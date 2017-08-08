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
        //private static Session session;
        private static List<Session> sessions;
        public static List<Session> Sessions { get { return sessions; } }
        //public static Session Session { get { return session; } }
        static void Main(string[] args)
        {
            try
            {
                Logging.TurnOff = false;
                Logging.Initialize("as.log");
                //test_expect(args); new Regex(@"[a-zA-Z]:[^>\n]*?>")));
                Boolean expectP2P = true;
                Boolean expectSAP = true;
                if (args.Length > 0)
                {
                    if (args[0] == "--no_p2p")
                    {
                        expectP2P = false;
                    }
                    else if (args[0] == "--no_sap")
                    {
                        expectSAP = false;
                    }
                    else if (args[0] == "--no_all")
                    {
                        expectP2P = false;
                        expectSAP = false;
                    }
                }
                sessions = new List<Session>();
                var expect_programs = new List<Dictionary<string, string>>();
                if (expectP2P)
                {
                    expect_programs.Add(new Dictionary<string, string> { { "program", @"P2PApplication.exe" }, { "expect", @"\n>" }, });
                }
                if (expectSAP)
                {
                    expect_programs.Add(new Dictionary<string, string> { { "program", @"WiFiDirectLegacyAPDemo.exe" }, { "expect", @"\n>" }, });
                }
                foreach (var run_program in expect_programs)
                {
                    var expect_program = run_program["program"];
                    var expect_regex = new Regex(run_program["expect"]);
                    var sess = Expect.Spawn(new ProcessSpawnable(expect_program), expect_regex);
                    sessions.Add(sess);
                    string banner = sess.ClearBuffer(2000);
                    Console.WriteLine("Cmd started with banner:\n" + banner + "!BANNER_END!");
                    Console.WriteLine(String.Format("encode type={0}", Console.OutputEncoding.CodePage));
                }
                start_as(args);
            }
            catch(Exception err)
            {
                Logging.WriteLine(String.Format("%Fatal error%: as exception occurred: {0}", err));
            }
        }

        static void start_as(string[] args)
        {
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

        //static void test_expect(string[] args)
        //{
        //    try
        //    {
                
        //        var result = session.Cmd("ping 127.0.0.1 -n 1", 2);
        //        Console.WriteLine("buffer: <!" + session.OutputBuffer + "!>");
        //        Console.WriteLine("result: <!" + result + "!>");
        //        result = session.Cmd("ipconfig", 4);
        //        Console.WriteLine("buffer: <!" + session.OutputBuffer + "!>");
        //        Console.WriteLine("result: <!" + result + "!>");
        //        result = session.Cmd("ping 127.0.0.1 -n 2", 3 );
        //        Console.WriteLine("buffer: <!" + session.OutputBuffer + "!>");
        //        Console.WriteLine("result: <!" + result + "!>");
        //        Console.WriteLine("remains: <!" + session.ClearBuffer(3000) + "!>");
        //        //spawn.Send("net user\n");
        //        //spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("net user found:" + s + "!"));
        //        //spawn.Timeout = 5000;
        //        //spawn.Send("ping 127.0.0.1 -n 3\n");
        //        //spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("ping found:" + s + "!"));
        //        //spawn.Send("cd c:\\Users\n");
        //        //spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("cd found:" + s + "!"));
        //        //spawn.Send("dir c:\\\n");
        //        //spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), (s) => Console.WriteLine("dir c: found: " + s + "!"));
        //        //spawn.Send("asdsdf\n");
        //        //spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), (s) => Console.WriteLine("asdsdf found: " + s + "!"));
        //        //spawn.Send("ping 127.0.0.1 -n 10\n");
        //        //spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("ping found:" + s + "!"), 15000);
        //        //spawn.Send("c:\n");

        //        //spawn.Expect(@">", s => spawn.Send("cd Users\n"));

        //        //spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), (s) => Console.WriteLine("c: found: " + s + "!"));
        //        //spawn.Send("whoami\n");
        //        //spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("whoami found:" + s + "!"));
        //        //spawn.Send("cd c:\\Users\n");
        //        //spawn.Expect(new Regex(@"[a-zA-Z]:[^>\n]*?>"), s => Console.WriteLine("cd found:" + s + "!"));

        //    }
        //    catch (Exception e)
        //    {
        //        Console.Error.WriteLine(e);
        //    }
        //    Console.ReadKey();
        //}
    }
}
