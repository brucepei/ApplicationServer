using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ApplicationServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Logging.Initialize("./as.log");
            var appServ = new AppServer(16789);
            appServ.Start();
        }
    }
}
