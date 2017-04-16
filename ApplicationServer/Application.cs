using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading.Tasks;

namespace ApplicationServer
{
    public struct CommandResult
    {
        public CommandResult(string o, string e, int exit_code)
        {
            Output = o;
            Error = e;
            ExitCode = exit_code;
        }
        public string Output;
        public string Error;
        public int ExitCode;
    }

    class Application
    {
        public Application(String cmd)
        {
            command = cmd;
        }

        private String command;
        public String Command
        {
            get { return command; }
            set { command = value; }
        }

        private String program;
        public String Program
        {
            get { return program; }
            set { program = value; }
        }

        private String arguments;
        public String Arguments
        {
            get { return arguments; }
            set { arguments = value; }
        }

        [DllImport("shell32.dll", SetLastError = true)]
        static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);

        public static string[] CommandLineToArgs(string commandLine)
        {
            string executableName;
            return CommandLineToArgs(commandLine, out executableName);
        }

        public static string[] CommandLineToArgs(string commandLine, out string executableName)
        {
            int argCount;
            IntPtr result;
            string arg;
            IntPtr pStr;
            result = CommandLineToArgvW(commandLine, out argCount);
            if (result == IntPtr.Zero)
            {
                throw new System.ComponentModel.Win32Exception();
            }
            else
            {
                try
                {
                    // Jump to location 0*IntPtr.Size (in other words 0).
                    pStr = Marshal.ReadIntPtr(result, 0 * IntPtr.Size);
                    executableName = Marshal.PtrToStringUni(pStr);

                    // Ignore the first parameter because it is the application
                    // name which is not usually part of args in Managed code.
                    string[] args = new string[argCount-1];
                    for (int i = 0; i < args.Length; i++)
                    {
                        pStr = Marshal.ReadIntPtr(result, (i+1) * IntPtr.Size);
                        arg = Marshal.PtrToStringUni(pStr);
                        args[i] = arg;
                    }
                    return args;
                }
                finally
                {
                    Marshal.FreeHGlobal(result);
                }
            }
        }

        public static bool ExistsOnPath(string fileName)
        {
            return GetFullPath(fileName) != null;
        }

        public static String[] PossibleFileNames(string fileName)
        {
            String[] result;
            var exeName = fileName.ToLower();
            if (exeName.EndsWith(".exe") || exeName.EndsWith(".bat"))
            {
                result = new String[] {fileName};
            }
            else
            {
                result = new String[] { fileName + ".exe", fileName + ".bat" };
            }
            return result;
        }

        public static string GetFullPath(string fileName)
        {
            String[] names = PossibleFileNames(fileName);
            foreach (var fn in names)
            {
                if (File.Exists(fn))
                    return Path.GetFullPath(fn);

                var values = Environment.GetEnvironmentVariable("PATH");
                foreach (var path in values.Split(';'))
                {
                    var fullPath = Path.Combine(path, fn);
                    if (File.Exists(fullPath))
                        return fullPath;
                }
            }
            return null;
        }

        private void SplitCommand(Boolean shellExecute=false)
        {
            command = command.Trim();
            string executableName;
            String[] args = CommandLineToArgs(command, out executableName);
            if (command.StartsWith("\"") && command.EndsWith("\"") && executableName == command.Substring(1, command.Length-2))
            {
                command = command.Trim('"').Trim();
                Logging.WriteLine("Command with doublequote, so remove them, and parse again!");
                args = CommandLineToArgs(executableName, out executableName);
            }
            if (shellExecute)
            {
                program = "cmd.exe";
                arguments = "/c " + command;
            }
            else
            {
                Logging.WriteLine(String.Format("Parse command '{0}': exename: {1}, args: {2}", command, executableName, args));
                if (String.IsNullOrWhiteSpace(executableName))
                {
                    throw new ArgumentException(String.Format("Cannot find executable program in command: {0}!", command));
                }
                else
                {
                    String exeFullName = GetFullPath(executableName);
                    if (String.IsNullOrEmpty(exeFullName))
                    {
                        throw new ArgumentException(String.Format("Cannot find executable file: {0}!", executableName));
                    }
                    foreach (var arg in args)
                    {
                        arguments = arguments + "\"" + arg + "\" ";
                    }
                    program = exeFullName;
                    arguments = arguments.Trim();
                }
            }
        }

        public CommandResult Run(Int32 timeout=0)
        {
            try
            {
                SplitCommand();
            }
            catch (Exception ex)
            {
                Logging.WriteLine("Cannot recognize the program, so run as shell command: " + ex.Message);
                SplitCommand(true);
            }
            Process p = new Process();
            p.StartInfo.FileName = program;
            p.StartInfo.Arguments = arguments;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.Start();
            //string output = p.StandardOutput.ReadToEnd();
            //string error = p.StandardError.ReadToEnd();
            //Logging.WriteLine(String.Format("Got app output: {0}, error: {1}!", output.Length, error.Length));
            //p.WaitForExit();
            string output = null;
            string error = null;
            var t_out = new Task<String>(process => output = ReadProcessOutput((Process)process), p);
            var t_err = new Task<String>(process => error = ReadProcessError((Process)process), p);
            t_out.Start();
            t_err.Start();
            if (timeout > 0)
            {
                while (!p.WaitForExit(timeout * 1000))
                {
                    Logging.WriteLine("Process not exited, kill it!");
                    p.Kill();
                }
            }
            else
            {
                p.WaitForExit();
            }
            try
            {
                Logging.WriteLine(String.Format("Got app output: {0}, error: {1}!", t_out.Result.Length, t_err.Result.Length));
            }
            catch (Exception ex)
            {
                Logging.WriteLine("Failed to capture App's output and error: " + ex.InnerException.Message);
            }
            return new CommandResult(output, error, p.ExitCode);
        }

        public String ReadProcessOutput(Process p)
        {
            StringBuilder result = new StringBuilder();
            Int32 onceRead = 4096;
            Char[] buff = new Char[onceRead];
            Int32 readLength = 0;
            while ((readLength = p.StandardOutput.ReadBlock(buff, 0, onceRead)) == onceRead)
            {
                for (int i = 0; i < readLength; i++)
                {
                    result.Append(buff[i]);
                }
                //Logging.WriteLine("Read block: " + readLength);
            }
            if (readLength > 0)
            {
                for (int i = 0; i < readLength; i++)
                {
                    result.Append(buff[i]);
                }
            }
            return result.ToString();
        }

        public String ReadProcessError(Process p)
        {
            StringBuilder result = new StringBuilder();
            Int32 onceRead = 4096;
            Char[] buff = new Char[onceRead];
            Int32 readLength = 0;
            while ((readLength = p.StandardError.ReadBlock(buff, 0, onceRead)) == onceRead)
            {
                for (int i = 0; i < readLength; i++)
                {
                    result.Append(buff[i]);
                }
            }
            if (readLength > 0)
            {
                for (int i = 0; i < readLength; i++)
                {
                    result.Append(buff[i]);
                }
            }
            return result.ToString();
        }
    }
}
