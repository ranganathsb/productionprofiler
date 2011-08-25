﻿
using System;
using System.Collections.Generic;
using System.Threading;
using ProductionProfiler.Core.Profiling.Entities;

namespace ProductionProfiler.Core.Persistence
{
    /// <summary>
    /// Implementation of a producer / consumer queue to process the persistence of 
    /// collected data to the underlying data store asynchronously via a background worker thread.
    /// </summary>
    internal class PersistenceWorkerQueue
    {
        private readonly Thread _workerthread;
        private readonly object _syncLock = new object();
        private readonly Queue<IAsyncPersistable> _queue = new Queue<IAsyncPersistable>();
        private readonly EventWaitHandle _waitHandle = new AutoResetEvent(false);
        private readonly IDictionary<Type, Action<IAsyncPersistable>> _typeActionMappings;

        internal PersistenceWorkerQueue(IDictionary<Type, Action<IAsyncPersistable>> typeActionMappings)
        {
            _typeActionMappings = typeActionMappings;
            _workerthread = new Thread(ProcessOutgoing)
            {
                IsBackground = true
            };

            _workerthread.Start();
        }

        internal void Enqueue(IAsyncPersistable data)
        {
            lock (_syncLock)
            {
                _queue.Enqueue(data);
            }
            _waitHandle.Set();
        }

        private void ProcessOutgoing()
        {
            while (true)
            {
                IAsyncPersistable data = null;

                lock (_syncLock)
                {
                    if (_queue.Count > 0)
                    {
                        data = _queue.Dequeue();

                        //if a null object is added to the queue, its a signal to stop the worker thread
                        if (data == null) 
                            return;
                    }
                }

                if (data != null)
                {
                    var dataType = data.GetType();

                    if(_typeActionMappings.ContainsKey(dataType))
                    {
                        _typeActionMappings[dataType](data);
                    }
                }
                else
                {
                    //if data was not found (meaning list is empty) then wait till something is added to it
                    _waitHandle.WaitOne();
                }
            }
        }
    }
}
