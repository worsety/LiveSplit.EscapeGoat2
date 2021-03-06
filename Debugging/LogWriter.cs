﻿using System;
using System.IO;
using System.Collections.Generic;

namespace LiveSplit.EscapeGoat2.Debugging
{
    public static class LogWriter
    {
        private static Object locker = new Object();
        public static void WriteLine(string format, params object[] arg) {
#if DEBUG
            lock (locker) {
                try {
                    string str = format;
                    if (arg.Length > 0)
                        str = String.Format(format, arg);

                    StreamWriter wr = new StreamWriter("_goatauto.log", true);
                    wr.WriteLine("[{0:yyyy-MM-dd HH:mm:ss.fff}] {1}", DateTime.Now, str);
                    wr.Close();
                } catch (Exception) {}
            }
#endif
        }
    }
}
