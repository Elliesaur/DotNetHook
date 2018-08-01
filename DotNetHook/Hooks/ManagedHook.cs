using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotNetHook.Models;

namespace DotNetHook.Hooks
{
    public class ManagedHook : HookBase
    {
        #region Public Properties

        /// <summary>
        ///     The absolutely original function pointer data for the from method.
        /// </summary>
        public byte[] FromPtrData { get; private set; }

        /// <summary>
        ///     The method to redirect from.
        /// </summary>
        public MethodBase FromMethod { get; }

        /// <summary>
        ///     The method to redirect to.
        /// </summary>
        public MethodBase ToMethod { get; }

        #endregion

        #region Fields

        private byte[] _existingPtrData;
        private byte[] _originalPtrData;

        private IntPtr _toPtr;
        private IntPtr _fromPtr;

        #endregion

        #region Constructors

        /// <summary>
        ///     Create a new instance of <see cref="ManagedHook" /> with the specified target method to hook and the callback
        ///     method to call.
        /// </summary>
        /// <param name="from">The target method to redirect.</param>
        /// <param name="to">The method to call when the target method is called.</param>
        public ManagedHook(MethodBase from, MethodBase to)
        {
            FromMethod = from;
            ToMethod = to;
        }

        #endregion

        #region Public Methods

        /// <summary>
        ///     Apply the hook to the methods supplied.
        /// </summary>
        public override void Apply()
        {
            Redirect(FromMethod.MethodHandle, ToMethod.MethodHandle);
            IsEnabled = true;
        }

        /// <summary>
        ///     Reapply the hook to the methods supplied.
        /// </summary>
        public override void ReApply()
        {
            if (_existingPtrData == null)
                throw new NullReferenceException(
                    "ExistingPtrData was null. Call ManagedHook.Remove() to populate the data.");

            VirtualProtect(_fromPtr, (IntPtr)5, 0x40, out uint x);

            for (var i = 0; i < _existingPtrData.Length; i++)
                // Instead write back the contents from the existing ptr data first applied.
                Marshal.WriteByte(_fromPtr, i, _existingPtrData[i]);

            VirtualProtect(_fromPtr, (IntPtr) 5, x, out x);
            IsEnabled = true;
        }

        /// <summary>
        ///     Remove the hook from the methods supplied.
        /// </summary>
        public override void Remove()
        {
            if (_originalPtrData == null)
                throw new NullReferenceException(
                    "OriginalPtrData was null. Call ManagedHook.Apply() to populate the data.");

            // Unlock memory for readwrite
            VirtualProtect(_fromPtr, (IntPtr)5, 0x40, out uint x);

            _existingPtrData = new byte[_originalPtrData.Length];

            for (var i = 0; i < _originalPtrData.Length; i++)
            {
                // Add to the existing ptr data variable so we can reapply if need be.
                _existingPtrData[i] = Marshal.ReadByte(_fromPtr, i);

                Marshal.WriteByte(_fromPtr, i, _originalPtrData[i]);
            }

            VirtualProtect(_fromPtr, (IntPtr) 5, x, out x);
            IsEnabled = false;
        }

        public new T Call<T>(object instance, params object[] args) where T : class
        {
            Remove();
            try
            {
                var ret = FromMethod.Invoke(instance, args) as T;
                ReApply();
                return ret;
            }
            catch (Exception)
            {
                // TODO: On Hook failure, raise an event, or called a logger.
            }

            ReApply();
            return default(T);

        }

        #endregion

        #region Private Methods

        private void Redirect(RuntimeMethodHandle from, RuntimeMethodHandle to)
        {
            RuntimeHelpers.PrepareMethod(from);
            RuntimeHelpers.PrepareMethod(to);


            // Just in case someone calls apply twice or something, let's not get the same ptr for no reason.
            if (_fromPtr == default(IntPtr)) _fromPtr = from.GetFunctionPointer();
            if (_toPtr == default(IntPtr)) _toPtr = to.GetFunctionPointer();

            FromPtrData = new byte[32];
            Marshal.Copy(_fromPtr, FromPtrData, 0, 32);

            VirtualProtect(_fromPtr, (IntPtr) 5, 0x40, out uint x);

            if (IntPtr.Size == 8)
            {
                // x64

                _originalPtrData = new byte[13];

                // 13
                Marshal.Copy(_fromPtr, _originalPtrData, 0, 13);

                Marshal.WriteByte(_fromPtr, 0, 0x49);
                Marshal.WriteByte(_fromPtr, 1, 0xbb);

                Marshal.WriteInt64(_fromPtr, 2, _toPtr.ToInt64());

                Marshal.WriteByte(_fromPtr, 10, 0x41);
                Marshal.WriteByte(_fromPtr, 11, 0xff);
                Marshal.WriteByte(_fromPtr, 12, 0xe3);

            }
            else if (IntPtr.Size == 4)
            {
                // x86

                _originalPtrData = new byte[6];

                // 6
                Marshal.Copy(_fromPtr, _originalPtrData, 0, 6);

                Marshal.WriteByte(_fromPtr, 0, 0xe9);
                Marshal.WriteInt32(_fromPtr, 1, _toPtr.ToInt32() - _fromPtr.ToInt32() - 5);
                Marshal.WriteByte(_fromPtr, 5, 0xc3);
            }

            VirtualProtect(_fromPtr, (IntPtr) 5, x, out x);
        }

        [DllImport("kernel32.dll")]
        private static extern bool VirtualProtect(IntPtr lpAddress, IntPtr dwSize, uint flNewProtect,
                                                  out uint lpflOldProtect);

        #endregion
    }
}