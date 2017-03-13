using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ApplicationServer
{
    public static class Logging
    {
        private static Dictionary<long, long> lockDic = new Dictionary<long, long>();
        private static string _fileName;
        public static string FileName
        {
            get { return _fileName; }
            set { _fileName = value; }
        }
        private static Boolean turnOff;
        public static Boolean TurnOff
        {
            get { return turnOff; }
            set { turnOff = value; }
        }

        public static void Initialize(string fileName)
        {
            if (turnOff)
            {
                return;
            }
            if (string.IsNullOrEmpty(fileName))
            {
                throw new Exception("FileName should not be empty!");
            }
            Create(fileName);
            _fileName = fileName;
        }

        private static void Create(string fileName)
        {
            var directoryPath = Path.GetDirectoryName(fileName);
            if (string.IsNullOrEmpty(directoryPath))
            {
                throw new Exception("FileName should not be null！");
            }
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        private static void Write(string content, string newLine)
        {
            if (String.IsNullOrEmpty(_fileName))
            {
                throw new Exception("%Error: need initialize Logging at first!");
            }
            var logLine = DateTime.Now.ToLocalTime().ToString() + " " + content + newLine;
            Console.Write(logLine);
            using (FileStream fs = new FileStream(_fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 8, FileOptions.Asynchronous))
            {
                Byte[] dataArray = Encoding.UTF8.GetBytes(logLine);
                bool flag = true;
                long slen = dataArray.Length;
                long len = 0;
                while (flag)
                {
                    try
                    {
                        if (len >= fs.Length)
                        {
                            fs.Lock(len, slen);
                            lockDic[len] = slen;
                            flag = false;
                        }
                        else
                        {
                            len = fs.Length;
                        }
                    }
                    catch (Exception)
                    {
                        while (!lockDic.ContainsKey(len))
                        {
                            len += lockDic[len];
                        }
                    }
                }
                fs.Seek(len, SeekOrigin.Begin);
                fs.Write(dataArray, 0, dataArray.Length);
                fs.Close();
            }
        }

        public static void WriteLine(string content)
        {
            if (turnOff)
            {
                return;
            }
            Write(content, Environment.NewLine);
        }

        public static void Write(string content)
        {
            if (turnOff)
            {
                return;
            }
            Write(content, "");
        }
    }
}
