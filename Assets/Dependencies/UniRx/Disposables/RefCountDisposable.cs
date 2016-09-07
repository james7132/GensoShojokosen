﻿using System;
using System.Diagnostics.CodeAnalysis;

namespace UniRx {

    /// <summary> Represents a disposable resource that only disposes its underlying disposable resource when all
    /// <see cref="GetDisposable"> dependent disposable objects </see> have been disposed. </summary>
    public sealed class RefCountDisposable : ICancelable {

        sealed class InnerDisposable : IDisposable {

            private RefCountDisposable _parent;
            object parentLock = new object();

            public InnerDisposable(RefCountDisposable parent) { _parent = parent; }

            public void Dispose() {
                RefCountDisposable parent;
                lock (parentLock) {
                    parent = _parent;
                    _parent = null;
                }
                if (parent != null)
                    parent.Release();
            }

        }

        private readonly object _gate = new object();
        private IDisposable _disposable;
        private bool _isPrimaryDisposed;
        private int _count;

        /// <summary> Initializes a new instance of the <see cref="T:System.Reactive.Disposables.RefCountDisposable" /> class with
        /// the specified disposable. </summary>
        /// <param name="disposable"> Underlying disposable. </param>
        /// <exception cref="ArgumentNullException"> <paramref name="disposable" /> is null. </exception>
        public RefCountDisposable(IDisposable disposable) {
            if (disposable == null)
                throw new ArgumentNullException("disposable");

            _disposable = disposable;
            _isPrimaryDisposed = false;
            _count = 0;
        }

        /// <summary> Gets a value that indicates whether the object is disposed. </summary>
        public bool IsDisposed {
            get { return _disposable == null; }
        }

        /// <summary> Disposes the underlying disposable only when all dependent disposables have been disposed. </summary>
        public void Dispose() {
            IDisposable disposable = default(IDisposable);
            lock (_gate) {
                if (_disposable != null) {
                    if (!_isPrimaryDisposed) {
                        _isPrimaryDisposed = true;

                        if (_count == 0) {
                            disposable = _disposable;
                            _disposable = null;
                        }
                    }
                }
            }

            if (disposable != null)
                disposable.Dispose();
        }

        /// <summary> Returns a dependent disposable that when disposed decreases the refcount on the underlying disposable. </summary>
        /// <returns> A dependent disposable contributing to the reference count that manages the underlying disposable's lifetime. </returns>
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate",
            Justification = "Backward compat + non-trivial work for a property getter.")]
        public IDisposable GetDisposable() {
            lock (_gate) {
                if (_disposable == null) {
                    return Disposable.Empty;
                }
                else {
                    _count++;
                    return new InnerDisposable(this);
                }
            }
        }

        private void Release() {
            IDisposable disposable = default(IDisposable);
            lock (_gate) {
                if (_disposable != null) {
                    _count--;

                    if (_isPrimaryDisposed) {
                        if (_count == 0) {
                            disposable = _disposable;
                            _disposable = null;
                        }
                    }
                }
            }

            if (disposable != null)
                disposable.Dispose();
        }

    }

    public partial class Observable {

        static IObservable<T> AddRef<T>(IObservable<T> xs, RefCountDisposable r) {
            return
                Create<T>(
                    (IObserver<T> observer) =>
                        new CompositeDisposable(new IDisposable[] {r.GetDisposable(), xs.Subscribe(observer)}));
        }

    }

}