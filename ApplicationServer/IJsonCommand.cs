using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AS.CommandProtocol
{
    interface IJsonCommand
    {
        int ID { get; }
        string Version { get; }
        int Timeout { get; set; }
        CommandType Type { get; set; }
        string Command { get; set; }
        string Exception { get; set; }
    }

    interface IRunProgramCommand : IJsonCommand
    {
        string Output { get; set; }
        string Error { get; set; }
        int ExitCode { get; set; }
    }

    interface IExpectOutputCommand : IJsonCommand
    {
        string Output { get; set; }
    }
}
