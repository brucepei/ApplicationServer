using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandProtocol
{
    interface IJsonCommand
    {
        int ID { get; }
        int Timeout { get; set; }
        CommandType Type { get; set; }
        string Command { get; set; }
        string Exception { get; set; }
        string ToJson();
    }
}
