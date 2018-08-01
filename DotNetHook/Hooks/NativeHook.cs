using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotNetHook.Models;

namespace DotNetHook.Hooks
{
    public class NativeHook : HookBase
    {
        #region Public Properties

        /// <summary>
        ///     The native method to redirect from.
        /// </summary>
        public NativeMethod FromMethod { get; }

        /// <summary>
        ///     The managed method to redirect to.
        /// </summary>
        public MethodBase ToMethod { get; }

        /// <summary>
        ///     The absolutely original function pointer data.
        /// </summary>
        public byte[] FromPtrData { get; private set; }

        #endregion

        #region Fields

        /// <summary>
        ///     The module cache.
        /// </summary>
        private readonly Dictionary<string, IntPtr> _modules = new Dictionary<string, IntPtr>();

        /// <summary>
        ///     The function pointer data for the redirect, created after <see cref="Remove" /> is called.
        /// </summary>
        private byte[] _existingPtrData;

        /// <summary>
        ///     The original function pointer data.
        /// </summary>
        private byte[] _originalPtrData;

        #endregion

        #region Constructors

        /// <summary>
        ///     Create a new native to managed hook between a native method and a managed method.
        /// </summary>
        /// <param name="from">The <see cref="NativeMethod" /> to hook.</param>
        /// <param name="to">The method to use in place.</param>
        public NativeHook(NativeMethod from, MethodBase to)
        {
            FromMethod = from;
            FromMethod.Address = GetAddress(from.ModuleName, from.Method);

            if (FromMethod.Address == IntPtr.Zero) throw new ArgumentException(nameof(from));

            ToMethod = to;
        }

        #endregion

        #region Public Methods

        /// <summary>
        ///     Apply the hook to the methods supplied.
        /// </summary>
        public override void Apply()
        {
            Redirect(FromMethod, ToMethod.MethodHandle);
            IsEnabled = true;
        }

        /// <summary>
        ///     Reapply the hook to the methods supplied.
        /// </summary>
        public override void ReApply()
        {
            var fromPtr = FromMethod.Address;

            VirtualProtect(fromPtr, (IntPtr) 5, 0x40, out uint x);

            for (var i = 0; i < _existingPtrData.Length; i++)
                // Instead write back the contents from the existing ptr data first applied.
                Marshal.WriteByte(fromPtr, i, _existingPtrData[i]);

            VirtualProtect(fromPtr, (IntPtr) 5, x, out x);
            IsEnabled = true;
        }

        /// <summary>
        ///     Remove the hook from the methods supplied.
        /// </summary>
        public override void Remove()
        {
            var fromPtr = FromMethod.Address;

            // Unlock memory for readwrite
            VirtualProtect(fromPtr, (IntPtr) 5, 0x40, out uint x);

            _existingPtrData = new byte[_originalPtrData.Length];

            for (var i = 0; i < _originalPtrData.Length; i++)
            {
                // Add to the existing ptr data variable so we can reapply if need be.
                _existingPtrData[i] = Marshal.ReadByte(fromPtr, i);

                Marshal.WriteByte(fromPtr, i, _originalPtrData[i]);
            }

            VirtualProtect(fromPtr, (IntPtr) 5, x, out x);
            IsEnabled = false;
        }

        /// <summary>
        ///     Calls the original native method. Only usable in <see cref="NativeHook" />.
        /// </summary>
        /// <typeparam name="T">The delegate to use as a signature.</typeparam>
        /// <typeparam name="V">The return type.</typeparam>
        /// <param name="args">Arguments for the original native method.</param>
        /// <returns>The result from the native method.</returns>
        public new V Call<T, V>(params object[] args)
            where T : class
            where V : class
        {
            Remove();
            var ret = Marshal.GetDelegateForFunctionPointer(FromMethod.Address, typeof(T)).DynamicInvoke(args) as V;
            ReApply();
            return ret;
        }

        #endregion

        #region Private Methods

        private IntPtr GetAddress(string module, string method)
        {
            if (!_modules.ContainsKey(module)) _modules.Add(module, LoadLibraryEx(module, IntPtr.Zero, 0));
            return GetProcAddress(_modules[module], method);
        }

        private void Redirect(NativeMethod from, RuntimeMethodHandle to)
        {
            RuntimeHelpers.PrepareMethod(to);


            var fromPtr = from.Address;
            var toPtr = to.GetFunctionPointer();

            FromPtrData = new byte[32];
            Marshal.Copy(fromPtr, FromPtrData, 0, 32);

            VirtualProtect(fromPtr, (IntPtr) 5, 0x40, out uint x);

            if (IntPtr.Size == 8)
            {
                // x64
                _originalPtrData = new byte[13];

                Marshal.Copy(fromPtr, _originalPtrData, 0, 13);

                Marshal.WriteByte(fromPtr, 0, 0x49);
                Marshal.WriteByte(fromPtr, 1, 0xbb);

                Marshal.WriteInt64(fromPtr, 2, toPtr.ToInt64());

                Marshal.WriteByte(fromPtr, 10, 0x41);
                Marshal.WriteByte(fromPtr, 11, 0xff);
                Marshal.WriteByte(fromPtr, 12, 0xe3);

            }
            else if (IntPtr.Size == 4)
            {
                // x86

                _originalPtrData = new byte[6];

                Marshal.Copy(fromPtr, _originalPtrData, 0, 6);

                Marshal.WriteByte(fromPtr, 0, 0xe9);
                Marshal.WriteInt32(fromPtr, 1, toPtr.ToInt32() - fromPtr.ToInt32() - 5);
                Marshal.WriteByte(fromPtr, 5, 0xc3);
            }

            VirtualProtect(fromPtr, (IntPtr) 5, x, out x);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr lpAddress, IntPtr dwSize, uint flNewProtect,
                                                  out uint lpflOldProtect);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hReservedNull, uint dwFlags);

        #endregion
    }

    public class NativeMethod
    {
        #region Fields

        /// <summary>
        ///     The address, filled later on.
        /// </summary>
        public IntPtr Address;

        /// <summary>
        ///     The method name.
        /// </summary>
        public string Method;

        /// <summary>
        ///     The module name. Including the file extension.
        /// </summary>
        public string ModuleName;

        #endregion

        #region Constructors

        /// <summary>
        ///     Create a new NativeMethod instance for a native hook to use.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="module"></param>
        public NativeMethod(string method, string module)
        {
            ModuleName = module;
            Method = method;
        }

        #endregion

        #region Public Methods

        public override string ToString()
        {
            return $"{ModuleName} - {Method} ({Address})";
        }

        #endregion
    }
}