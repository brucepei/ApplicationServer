using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ExpectNet
{
    public class Expect
    {
        public static Session Spawn(ISpawnable spawnable) 
        {
            spawnable.Init();
            return new Session(spawnable);
        }

        /// <summary>
        /// Spawn an Expect session.
        /// </summary>
        /// <example>
        /// sess = Expect.Spawn(new ProcessSpawnable("cmd.exe"), new Regex(@"[a-zA-Z]:[^>\n]*?>"));
        /// </example>
        /// <param name="spawnable">Instance of ISpawnable</param>
        /// <param name="default_cmd_regex">Default regular expression for Cmd()</param>
        /// <param name="default_timeout">Default timeout to expect, unit: seconds</param>
        public static Session Spawn(ISpawnable spawnable, Regex default_cmd_regex, float default_timeout=0f)
        {
            spawnable.Init();
            var sess = new Session(spawnable);
            if (default_timeout > 0)
            {
                sess.Timeout = (int)(default_timeout * 1000);
            }
            sess.DefCmdRegex = default_cmd_regex;
            return sess;
        }

    }
}
