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
        ClearExpectBuffer,
        ResetExpectSession,
    }

    public enum ExpectType
    {
        P2P,
        SAP,
    }

    static class JSON
    {
        public static string Stringify(object obj)
        {
            string result = null;
            MemoryStream ms = null;
            try
            {
                ms = new MemoryStream();
                DataContractJsonSerializer js = new DataContractJsonSerializer(obj.GetType());
                js.WriteObject(ms, obj);
                //ms.Position = 0;
                //StreamReader sr = new StreamReader(ms, Encoding.UTF8);
                //result = sr.ReadToEnd();
                result = Encoding.UTF8.GetString(ms.ToArray());
                Logging.WriteLine("{0} convert to Json: {1}", obj.GetType().FullName, result);
            }
            catch (Exception ex)
            {
                Logging.WriteLine("{0} failed to convert to Json: {1}", obj.GetType().FullName, ex.Message);
            }
            finally
            {
                if (ms != null)
                {
                    ms.Close();
                }
            }
            return result;
        }

        public static T Parse<T>(string json)
        {
            T result = default(T);
            MemoryStream ms = null;
            try
            {
                ms = new MemoryStream(Encoding.Unicode.GetBytes(json));
                DataContractJsonSerializer deseralizer = new DataContractJsonSerializer(typeof(T));
                result = (T)deseralizer.ReadObject(ms);
                Logging.WriteLine("Parsed {0} from Json: {1}", typeof(T).FullName, result);
            }
            catch (Exception ex)
            {
                Logging.WriteLine("Failed to parse {0} from Json '{1}': {2}", typeof(T).FullName, json, ex.Message);
            }
            finally
            {
                if (ms != null)
                {
                    ms.Close();
                }
            }
            return result;
        }
    }

    [DataContract]
    class JsonCommand : IRunProgramCommand, IExpectOutputCommand
    {
        private JsonCommand(CommandType type, string command, int timeout)
        {
            ID = autoIncreasedId++;
            Version = version;
            Type = type;
            Command = command;
            Timeout = timeout;
        }

        public static JsonCommand RunProgram(string command, int timeout)
        {
            var jc = new JsonCommand(CommandType.RunProgram, command, timeout);
            return jc;
        }

        public static JsonCommand ExpectOutput(string command, string regex_string, int timeout, ExpectType expect_type=ExpectType.P2P)
        {
            var jc = new JsonCommand(CommandType.ExpectOutput, command, timeout);
            jc.RegexString = regex_string;
            jc.ExpectType = expect_type;
            return jc;
        }

        public static JsonCommand ClearExpectBuffer(int timeout, ExpectType expect_type = ExpectType.P2P)
        {
            var jc = new JsonCommand(CommandType.ClearExpectBuffer, "", timeout);
            jc.ExpectType = expect_type;
            return jc;
        }

        public static JsonCommand ResetExpectSession(int timeout, ExpectType expect_type = ExpectType.P2P)
        {
            var jc = new JsonCommand(CommandType.ResetExpectSession, "", timeout);
            jc.ExpectType = expect_type;
            return jc;
        }

        static string version = "1.0";
        private int autoIncreasedId = 1;
        [DataMember]
        public int ID { get; set; }
        [DataMember]
        public string Version { get; set; }

        [DataMember]
        public CommandType Type { get; set; }
        [DataMember]
        public string Command { get; set; }
        [DataMember]
        public int Timeout { get; set; }
        [DataMember]
        public string RegexString { get; set; }
        [DataMember]
        public ExpectType ExpectType { get; set; }

        [DataMember]
        public string Output { get; set; }
        [DataMember]
        public string Error { get; set; }
        [DataMember]
        public string Exception { get; set; }
        [DataMember]
        public int ExitCode { get; set; }

        public override string ToString()
        {
            return String.Format("Version={0}, ID={1}, Type={2}, Command={3}, Timeout={4}, Output={5}, ExpectName={6}, Exception={7}", Version, ID, Type, Command, Timeout, Output, ExpectType, Exception);
        }

    }
}
