﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static ACCManager.ACCSharedMemory;

namespace ACCManager.Data.ACC.Tracker
{
    public class PageStaticTracker : IDisposable
    {
        private static PageStaticTracker _instance;
        public static PageStaticTracker Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new PageStaticTracker();

                return _instance;
            }
        }

        public event EventHandler<SPageFileStatic> Tracker;

        private bool isTracking = false;

        private readonly Task trackingTask;
        private readonly ACCSharedMemory sharedMemory;
        private SPageFileStatic _last = null;

        private PageStaticTracker()
        {
            sharedMemory = new ACCSharedMemory();

            trackingTask = Task.Run(() =>
            {
                isTracking = true;
                while (isTracking)
                {
                    Thread.Sleep(1);
                    SPageFileStatic next = sharedMemory.ReadStaticPageFile();
                    if (next != _last)
                        Tracker?.Invoke(this, next);
                }
            });

            _instance = this;
        }

        public void Dispose()
        {
            isTracking = false;
            trackingTask.Dispose();
        }
    }
}
