using ApplicationServer;
using System;
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
        private string _output_buffer;
        private int _timeout = 2500;
        private int _max_timeout_times = 5;
        private int _timeout_times = 0;
        internal Session(ISpawnable spawnable)
        {
            _spawnable = spawnable;
        }

        public Process Process { get { return _spawnable.Process; } }
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
        public Regex DefCmdRegex { get; set; }
        public string OutputBuffer { get { return _output_buffer; } }
        public int MaxTimeoutTimes
        {
            get
            {
                return _max_timeout_times;
            }

            set
            {
                _max_timeout_times = value;
            }
        }


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
        public bool Reset()
        {
            var result = false;
            if (Process.HasExited)
            {
                Logging.WriteLine(String.Format("Session process {0} has already exited at {1} with error code {2}!", Process.StartInfo.FileName, Process.ExitTime.ToString(), Process.ExitCode));
            }
            else
            {
                try
                {
                    Process.Kill();
                }
                catch (Win32Exception wexp)
                {
                    Logging.WriteLine(String.Format("Session process {0} failed to be killed: {1}!", Process.StartInfo.FileName, wexp));
                    return result;
                }
                catch (NotSupportedException nexp)
                {
                    Logging.WriteLine(String.Format("Session process {0} don't support 'kill': {1}!", Process.StartInfo.FileName, nexp));
                    return result;
                }
                catch (InvalidOperationException iexp)
                {
                    Logging.WriteLine(String.Format("Session process {0} is not existed or already exited: {1}!", Process.StartInfo.FileName, iexp));
                }
            }
            Process p = new Process();
            p.StartInfo.FileName = Process.StartInfo.FileName;
            p.StartInfo.Arguments = Process.StartInfo.Arguments;
            _spawnable = new ProcessSpawnable(p);
            _spawnable.Init();
            Logging.WriteLine(String.Format("Session program {0} has restarted, and clear the previous session's buffer <#{1}#>", Process.StartInfo.FileName, _output_buffer));
            _output_buffer = "";
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
        /// <exception cref="System.ArgumentNullException">Thrown when no default command 
        /// regular expression or regex_string</exception>
        /// <exception cref="System.TimeoutException">Thrown when query is not find for given
        /// amount of time</exception>
        public string Cmd(string command, double timeout_seconds=0, string regex_string=null)
        {
            var result = String.Empty;
            int timeout = (int)(timeout_seconds * 1000);
            Regex regex = null;
            if (String.IsNullOrEmpty(regex_string))
            {
                if (DefCmdRegex != null)
                {
                    regex = DefCmdRegex;
                }
                else
                {
                    throw new System.ArgumentNullException("No default command regular expression or regex_string!");
                }
            }
            else
            {
                regex = new Regex(regex_string);
                Logging.WriteLine(String.Format("Using user regex {0}", regex.ToString()));
            }
            Send(command + "\n");
            Logging.WriteLine(String.Format("Send command: <#{0}#>", command));
            try
            {
                Expect(regex, s => { result = s; }, timeout);
            }
            catch (System.TimeoutException)
            {
                Logging.WriteLine(String.Format("Run command <#{0}#> <#{1}#>ms timeout with result=<#{2}#>", command, timeout, result));
                _timeout_times++;
                var need_reset = false;
                if (Process.HasExited)
                {
                    Logging.WriteLine(String.Format("Session program {0} has exit at {1} with error code {1}, restart it!", Process.StartInfo.FileName, Process.ExitTime, Process.ExitCode));
                    need_reset = true;
                }
                else if (_timeout_times > MaxTimeoutTimes)
                {
                    Logging.WriteLine(String.Format("Session program {0} has reached the maximum timeout times: {1}, so restart session!", Process.StartInfo.FileName, MaxTimeoutTimes));
                    need_reset = true;
                }
                if (need_reset)
                {
                    Reset();
                    _timeout_times = 0;
                    String banner = ClearBuffer(2000);
                    Logging.WriteLine(String.Format("Restart session program {0} with banner:!BANNER_BEGIN!{1}!BANNER_END!", Process.StartInfo.FileName, banner));
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
            var result = _output_buffer;
            var tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;
            Task task = Task.Factory.StartNew(() =>
            {
                while (!ct.IsCancellationRequested)
                {
                    result += _spawnable.Read();
                }
            }, ct);
            if (!task.Wait(timeout, ct))
            {
                tokenSource.Cancel();
            }
            _output_buffer = "";
            return result;
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
            //_output_buffer = ""; //lpei: no need to clear buffer, the prevous buffer + new output
            var read_buffer = _output_buffer;
            bool expectedQueryFound = false;
            Task task = Task.Factory.StartNew(() =>
            {
                while (!ct.IsCancellationRequested && !expectedQueryFound)
                {
                    var perRead = _spawnable.Read();
                    var bytes = Encoding.UTF8.GetBytes(perRead);
                    byte[] new_bytes = new byte[bytes.Length];
                    Logging.WriteLine(String.Format("Raw read bytes: {0}", bytes));
                    var i = 0;
                    Logging.WriteLine(String.Format("Raw read: {0}", perRead));
                    foreach (var b in bytes)
                    {
                        if (b != 0)
                        {
                            new_bytes[i] = b;
                            i++;
                        }
                    }

                    perRead = Encoding.UTF8.GetString(new_bytes, 0, i);
                    Logging.WriteLine(String.Format("Modified read bytes: {0}", new_bytes));
                    Logging.WriteLine(String.Format("Modified read: {0}", perRead));
                    read_buffer += perRead;
                    expectedQueryFound = matcher.IsMatch(read_buffer);
                    if (expectedQueryFound)
                    {
                        Logging.WriteLine(String.Format("PreMatched: <#{0}#>", matcher.PreMatchedString));
                        Logging.WriteLine(String.Format("Matched: <#{0}#>", matcher.MatchedString));
                        Logging.WriteLine(String.Format("PostMatched: <#{0}#>", matcher.PostMatchedString));
                        read_buffer = matcher.PreMatchedString + matcher.MatchedString;
                        
                        _output_buffer = matcher.PostMatchedString;
                    }
                }
            }, ct);
            if (task.Wait(timeout, ct))
            {
                if (expectedQueryFound)
                {
                    Logging.WriteLine("Found expected output=PreMatched+Matched!");
                    handler(read_buffer);
                }
                else
                {
                    Logging.WriteLine(String.Format("Not found expected output from: <#{0}#>!", read_buffer));
                    _output_buffer = read_buffer;
                    handler("");
                } 
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
            _output_buffer = "";
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
                    _output_buffer += await task.ConfigureAwait(false);
                    expectedQueryFound = matcher.IsMatch(_output_buffer);
                    if (expectedQueryFound)
                    {
                        handler(_output_buffer);
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
