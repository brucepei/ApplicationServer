using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.IO;
using System.Runtime.Serialization.Json;
using ApplicationServer;

namespace CommandProtocol
{
    public enum CommandType
    {
        RunProgram,
        ExpectOutput,
    }

    [DataContract]
    abstract class BaseCommand : IJsonCommand
    {
        public BaseCommand(CommandType type, string command, int timeout)
        {
            ID = autoIncreasedId++;
            Type = type;
            Command = command;
            Timeout = timeout;
        }
        private int autoIncreasedId = 1;
        [DataMember]
        public int ID { get; set; }

        [DataMember]
        public CommandType Type { get; set; }

        [DataMember]
        public string Command { get; set; }

        [DataMember]
        public string Exception { get; set; }

        [DataMember]
        public int Timeout { get; set; }

        abstract public string Output { get; set; }

        public string ToJson()
        {
            string result = null;
            DataContractJsonSerializer js = new DataContractJsonSerializer(this.GetType());
            MemoryStream mem_fs = new MemoryStream();
            js.WriteObject(mem_fs, this);
            mem_fs.Position = 0;
            StreamReader sr = new StreamReader(mem_fs, Encoding.UTF8);
            result = sr.ReadToEnd();
            sr.Close();
            mem_fs.Close();
            return result;
        }

        public override string ToString()
        {
            return String.Format("ID={0}, Type={1}, Command={2}, Timeout={3}, Output={4}, Exception={5}", ID, Type, Command, Timeout, Output, Exception);
        }
    }

    [DataContract]
    class RunProgramCommand : BaseCommand
    {
        public RunProgramCommand(string command, int timeout)
            : base(CommandType.RunProgram, command, timeout)
        {
        }
        [DataMember]
        public override string Output { get; set; }

        [DataMember]
        public string Error { get; set; }

        [DataMember]
        public int ExitCode { get; set; }

        static public RunProgramCommand TryJson(string json)
        {
            RunProgramCommand result = null;
            using (var ms = new MemoryStream(Encoding.Unicode.GetBytes(json)))
            {
                DataContractJsonSerializer deseralizer = new DataContractJsonSerializer(typeof(RunProgramCommand));
                result = (RunProgramCommand)deseralizer.ReadObject(ms);
                Logging.WriteLine("Parsed RunProgramCommand Json: {0}", result);
            }
            return result;
        }
    }

    [DataContract]
    class ExpectOutputCommand : BaseCommand
    {
        public ExpectOutputCommand(string command, string regex_string, int timeout)
            : base(CommandType.ExpectOutput, command, timeout)
        {
            RegexString = regex_string;
        }

        [DataMember]
        public override string Output { get; set; }

        [DataMember]
        public string RegexString { get; set; }

        static public ExpectOutputCommand TryJson(string json)
        {
            ExpectOutputCommand result = null;
            using (var ms = new MemoryStream(Encoding.Unicode.GetBytes(json)))
            {
                DataContractJsonSerializer deseralizer = new DataContractJsonSerializer(typeof(ExpectOutputCommand));
                result = (ExpectOutputCommand)deseralizer.ReadObject(ms);
                Logging.WriteLine("Parsed ExpectOutputCommand Json: {0}", result);
            }
            return result;
        }
    }
}
