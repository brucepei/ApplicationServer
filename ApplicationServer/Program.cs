using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ApplicationServer
{
    class Program
    {
        static Int32 defaultPort = 16789;
        static void Main(string[] args)
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
    }
}
