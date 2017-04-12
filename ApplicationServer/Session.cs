﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ExpectNet
{
    /// <summary>
    /// Executes code when expected string is found by Expect function
    /// </summary>
    public delegate void ExpectedHandler();

    /// <summary>
    /// Executes code when expected string is found by Expect function.
    /// Receives session output to handle.
    /// </summary>
    /// <param name="output">session output with expected pattern</param>
    public delegate void ExpectedHandlerWithOutput(string output);

    
    public class Session
    {
        private ISpawnable _spawnable;
        private string _output;
        private int _timeout = 2500;
        private Regex default_regex;
        internal Session(ISpawnable spawnable)
        {
            _spawnable = spawnable;
        }

        public Process Process { get { return _spawnable.Process;} }

        /// <summary>
        /// Restart the spawned process of the session.
        /// </summary>
        /// <remarks>
        /// if the process has already exited, it will start process only.
        /// </remarks>
        /// <returns>true if restart successfully, or false</returns>
        /// <example>
        /// result = Reset();
        /// </example>
        public Boolean Reset()
        {
            var result = false;
            if (Process.HasExited)
            {
                Console.WriteLine(String.Format("Session process {0} has already exited at {1} with error code {2}!", Process.StartInfo.FileName, Process.ExitTime.ToString(), Process.ExitCode));
            }
            else
            {
                try
                {
                    Process.Kill();
                }
                catch (Win32Exception wexp)
                {
                    Console.WriteLine(String.Format("Session process {0} failed to be killed: {1}!", Process.StartInfo.FileName, wexp));
                    return result;
                }
                catch (NotSupportedException nexp)
                {
                    Console.WriteLine(String.Format("Session process {0} don't support 'kill': {1}!", Process.StartInfo.FileName, nexp));
                    return result;
                }
                catch (InvalidOperationException iexp)
                {
                    Console.WriteLine(String.Format("Session process {0} is not existed or already exited: {1}!", Process.StartInfo.FileName, iexp));
                }
            }
            Process p = new Process();
            p.StartInfo.FileName = Process.StartInfo.FileName;
            p.StartInfo.Arguments = Process.StartInfo.Arguments;
            _spawnable = new ProcessSpawnable(p);
            _spawnable.Init();
            result = true;
            return result;
        }

        /// <summary>
        /// Sends characters to the session.
        /// </summary>
        /// <remarks>
        /// To send enter you have to add '\n' at the end.
        /// </remarks>
        /// <example>
        /// Send("cmd.exe\n");
        /// </example>
        /// <param name="command">String to be sent to session</param>
        public void Send(string command)
        {
            _spawnable.Write(command);
        }

        public string Send(string command, float timeout_seconds, string regex_string = null, bool needCRLF = true)
        {
            var result = String.Empty;
            int timeout = (int)(timeout_seconds * 1000);
            if (needCRLF)
            {
                Send(command + "\n");
            }
            else
            {
                Send(command);
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
                Expect(regex, s => { result = s; }, timeout);
            }
            catch (System.TimeoutException)
            {
                Console.WriteLine("Timeout:" + command);
                if (Process.HasExited)
                {
                    Reset();
                }
                else
                {
                    Send("\n");
                    String buff = ClearBuffer(5000);
                    Console.WriteLine("Clear buffer: " + buff + "!BUFFER!");
                }
            }
            return result;
        }

        /// <summary>
        /// Read STDOUT and STDERR with desinated time, then clear buffer
        /// </summary>
        /// <returns>merge STDOUT and STDERR into a string, and return it</returns>
        /// <example>
        /// output = ClearBuffer(5000);
        /// </example>
        /// <param name="timeout">read time, unit: miliseconds</param>
        public string ClearBuffer(Int32 timeout)
        {
            var tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;
            _output = "";
            Task task = Task.Factory.StartNew(() =>
            {
                while (!ct.IsCancellationRequested)
                {
                    _output += _spawnable.Read();
                }
            }, ct);
            if (!task.Wait(timeout, ct))
            {
                tokenSource.Cancel();
            }
            return _output;
        }

        /// <summary>
        /// Waits until query is printed on session output and 
        /// executes handler
        /// </summary>
        /// <param name="query">expected output</param>
        /// <param name="handler">action to be performed</param>
        /// <param name="timeout">read time, unit: miliseconds</param>
        /// <exception cref="System.TimeoutException">Thrown when query is not find for given
        /// amount of time</exception>
        public void Expect(string query, ExpectedHandler handler, Int32 timeout = 0)
        {
            Expect(new StringContainsMatcher(query), (s) => handler(), timeout);
        }

        public void Expect(string query, ExpectedHandlerWithOutput handler, Int32 timeout = 0)
        {
            Expect(new StringContainsMatcher(query), (s) => handler(s), timeout);
        }

        public void Expect(Regex regex, ExpectedHandler handler, Int32 timeout = 0)
        {
            Expect(new RegexMatcher(regex), (s) => handler(), timeout);
        }

        public void Expect(Regex regex, ExpectedHandlerWithOutput handler, Int32 timeout = 0)
        {
            Expect(new RegexMatcher(regex), (s) => handler(s), timeout);
        }

        /// <summary>
        /// Waits until query is printed on session output and 
        /// executes handler. The output including expected query is
        /// passed to handler.
        /// </summary>
        /// <param name="query">expected output</param>
        /// <param name="handler">action to be performed, it accepts session output as ana argument</param>
        /// <exception cref="System.TimeoutException">Thrown when query is not find for given
        /// amount of time</exception>
        private void Expect(IMatcher matcher, ExpectedHandlerWithOutput handler, Int32 timeout=0)
        {
            var tokenSource = new CancellationTokenSource();
            if (timeout <= 0) {
                timeout = _timeout;
            }
            CancellationToken ct = tokenSource.Token;
            _output = "";
            bool expectedQueryFound = false;
            Task task = Task.Factory.StartNew(() =>
            {
                while (!ct.IsCancellationRequested && !expectedQueryFound)
                {
                    _output += _spawnable.Read();
                    expectedQueryFound = matcher.IsMatch(_output);
                }
            }, ct);
            if (task.Wait(timeout, ct))
            {
                handler(_output);
            }
            else
            {
                tokenSource.Cancel();
                throw new TimeoutException();
            }

        }
        /// <summary>
        /// Timeout value in miliseconds for Expect function
        /// </summary>
        public int Timeout
        {

            get { return _timeout; }


            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("Value must be larger than zero");
                }
                _timeout = value;
            }

        }

        public Regex Default_regex
        {
            get
            {
                return Default_regex1;
            }

            set
            {
                Default_regex1 = value;
            }
        }

        public Regex Default_regex1
        {
            get
            {
                return default_regex;
            }

            set
            {
                default_regex = value;
            }
        }

        /// <summary>
        /// Waits until query is printed on session output and 
        /// executes handler
        /// </summary>
        /// <param name="query">expected output</param>
        /// <param name="handler">action to be performed</param>
        /// <exception cref="System.TimeoutException">Thrown when query is not find for given
        /// amount of time</exception>
        public async Task ExpectAsync(string query, ExpectedHandler handler)
        {
            await ExpectAsync(new StringContainsMatcher(query), s => handler()).ConfigureAwait(false);
        }

        public async Task ExpectAsync(string query, ExpectedHandlerWithOutput handler)
        {
            await ExpectAsync(new StringContainsMatcher(query), s => handler(s)).ConfigureAwait(false);
        }

        public async Task ExpectAsync(Regex regex, ExpectedHandler handler)
        {
            await ExpectAsync(new RegexMatcher(regex), s => handler()).ConfigureAwait(false);
        }

        public async Task ExpectAsync(Regex regex, ExpectedHandlerWithOutput handler)
        {
            await ExpectAsync(new RegexMatcher(regex), s => handler(s)).ConfigureAwait(false);
        }

        /// <summary>
        /// Waits until query is printed on session output and 
        /// executes handler. The output including expected query is
        /// passed to handler.
        /// </summary>
        /// <param name="query">expected output</param>
        /// <param name="handler">action to be performed, it accepts session output as ana argument</param>
        /// <exception cref="System.TimeoutException">Thrown when query is not find for given
        /// amount of time</exception>
        private async Task ExpectAsync(IMatcher matcher, ExpectedHandlerWithOutput handler)
        {
            Task timeoutTask = null;
            if (_timeout > 0)
            {
                timeoutTask = Task.Delay(_timeout);
            }
            _output = "";
            bool expectedQueryFound = false;
            while (!expectedQueryFound)
            {
                Task<string> task = _spawnable.ReadAsync();
                IList<Task> tasks = new List<Task>();
                tasks.Add(task);
                if (timeoutTask != null)
                {
                    tasks.Add(timeoutTask);
                }
                Task any = await Task.WhenAny(tasks).ConfigureAwait(false);
                if (task == any)
                {
                    _output += await task.ConfigureAwait(false);
                    expectedQueryFound = matcher.IsMatch(_output);
                    if (expectedQueryFound)
                    {
                        handler(_output);
                    }
                }
                else
                {
                    throw new TimeoutException();
                }
            }
        }
    }
}
