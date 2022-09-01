﻿using ACCManager.Data.ACC.EntryList;
using ACCManager.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static ACCManager.ACCSharedMemory;

namespace ACCManager.Data.ACC.Tracker
{
    public class PageGraphicsTracker : IDisposable
    {
        private static PageGraphicsTracker _instance;
        public static PageGraphicsTracker Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new PageGraphicsTracker();

                return _instance;
            }
        }

        public event EventHandler<SPageFileGraphic> Tracker;

        private bool isTracking = false;

        private readonly Task trackingTask;
        private readonly ACCSharedMemory sharedMemory;

        private PageGraphicsTracker()
        {
            sharedMemory = new ACCSharedMemory();

            isTracking = true;

            trackingTask = Task.Run(() =>
            {
                while (isTracking)
                {
                    Thread.Sleep(1);
                    Tracker?.Invoke(this, sharedMemory.ReadGraphicsPageFile());
                }
            });

            Task.Run(() =>
            {
                while (isTracking)
                {
                    try
                    {
                        Thread.Sleep(100);

                        SPageFileGraphic sPageFileGraphic = sharedMemory.ReadGraphicsPageFile();

                        if (sPageFileGraphic.Status == AcStatus.AC_OFF)
                        {
                            if (BroadcastTracker.Instance.IsConnected)
                            {
                                BroadcastTracker.Instance.Disconnect();
#if DEBUG
                                EntryListTracker.Instance.Stop();
#endif
                            }

                            if (SetupHiderTracker.Instance.IsTracking)
                                SetupHiderTracker.Instance.Dispose();
                        }
                        else if (sPageFileGraphic.Status != AcStatus.AC_OFF)
                        {
                            if (!BroadcastTracker.Instance.IsConnected)
                            {
                                BroadcastTracker.Instance.Connect();
#if DEBUG
                                EntryListTracker.Instance.Start();
#endif
                            }

                            if (!SetupHiderTracker.Instance.IsTracking)
                                SetupHiderTracker.Instance.StartTracker();
                        }
                    }
                    catch (Exception e)
                    {
                        LogWriter.WriteToLog(e);
                        Debug.WriteLine(e);
                    }
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
