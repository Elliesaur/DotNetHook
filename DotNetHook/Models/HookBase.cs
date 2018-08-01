using System;

namespace DotNetHook.Models
{
    public abstract class HookBase : IDisposable
    {
        #region Public Properties

        /// <summary>
        ///     Whether the hook is enabled (active/applied).
        /// </summary>
        public bool IsEnabled { get; protected set; }

        #endregion

        #region Fields

        private bool _disposedValue;

        #endregion

        #region Public Methods

        /// <summary>
        ///     Apply the hook to the methods supplied.
        /// </summary>
        public abstract void Apply();

        /// <summary>
        ///     Remove the hook from the methods supplied.
        /// </summary>
        public abstract void Remove();

        /// <summary>
        ///     Reapply the hook to the methods supplied.
        /// </summary>
        public abstract void ReApply();

        /// <summary>
        ///     Calls the original managed method. Only usable in <see cref="Hooks.ManagedHook" />.
        /// </summary>
        /// <typeparam name="T">The return type of the method.</typeparam>
        /// <param name="instance">The instance of the class to use when invoked. Null if static.</param>
        /// <param name="args">Arguments for the original managed method.</param>
        /// <returns>The result from the managed method.</returns>
        public T Call<T>(object instance, params object[] args)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Calls the original native method. Only usable in <see cref="Hooks.NativeHook" />.
        /// </summary>
        /// <typeparam name="T">The delegate to use as a signature.</typeparam>
        /// <typeparam name="V">The return type.</typeparam>
        /// <param name="args">Arguments for the original native method.</param>
        /// <returns>The result from the native method.</returns>
        public V Call<T, V>(params object[] args)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected Methods

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                Remove();
                _disposedValue = true;
            }
        }

        #endregion

        #region Other

        ~HookBase()
        {
            Dispose(false);
        }

        #endregion
    }
}