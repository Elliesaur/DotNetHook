using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNetHook.Extensions
{
    public static class StringExtensions
    {
        #region Public Methods

        public static MethodInfo GetMethod(this string type, string method, BindingFlags flags = BindingFlags.Default,
                                           bool breakOnFind = true)
        {
            MethodInfo found = null;
            foreach (var mInfo in Type.GetType(type)
                                                  .GetMethods(flags))
            {
                if (mInfo.Name != method) continue;

                found = mInfo;
                if (breakOnFind)
                    break;
            }

            return found;
        }
        public static MethodInfo GetMethod(this string type, string method,
                                           Type[] parameterTypes, BindingFlags flags = BindingFlags.Default)
        {
           return Type.GetType(type).GetMethod(method, flags, null, parameterTypes, null);
        }

        /// <summary>
        /// Read an ascii string terminated by two null characters from the pointer's position.
        /// </summary>
        /// <param name="ptr">The pointer to the string to read.</param>
        /// <returns>The managed string located at the pointer.</returns>
        public static string ReadASCIINullTerminatedString(this IntPtr ptr)
        {
            List<byte> strBytes = new List<byte>();
            int count = 0;
            byte cur;

            // Terminated by 2ishx \0
            while (true)
            {
                cur = Marshal.ReadByte(ptr, count++);
                if (cur != 0x0)
                {
                    strBytes.Add(cur);
                }
                byte next = Marshal.ReadByte(ptr, count);

                // Reached end.
                if (next == 0x0 && cur == 0x0)
                {
                    break;
                }
            }

            string str = Encoding.ASCII.GetString(strBytes.ToArray());

            return str;
        }
        #endregion
    }
}