﻿using ACCManager.Broadcast;
using ACCManager.Controls.Telemetry.RaceSessions;
using ACCManager.Controls.Telemetry.RaceSessions.Plots;
using ACCManager.Data;
using ACCManager.Data.ACC.Cars;
using ACCManager.Data.ACC.Database;
using ACCManager.Data.ACC.Database.GameData;
using ACCManager.Data.ACC.Database.LapDataDB;
using ACCManager.Data.ACC.Database.SessionData;
using ACCManager.Data.ACC.Database.Telemetry;
using ACCManager.Data.ACC.Session;
using ACCManager.Data.ACC.Tracks;
using ACCManager.Util;
using LiteDB;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using TrackData = ACCManager.Data.ACC.Tracks.TrackNames.TrackData;

namespace ACCManager.Controls
{
    /// <summary>
    /// Interaction logic for RaceSessionBrowser.xaml
    /// </summary>
    public partial class RaceSessionBrowser : UserControl
    {
        public static RaceSessionBrowser Instance { get; private set; }
        private LiteDatabase CurrentDatabase;

        private int previousTelemetryComboSelection = -1;

        public RaceSessionBrowser()
        {
            InitializeComponent();

            this.Loaded += (s, e) => FindRaceWeekends();

            comboTracks.SelectionChanged += (s, e) => FillCarComboBox();
            comboCars.SelectionChanged += (s, e) => LoadSessionList();
            listViewRaceSessions.SelectionChanged += (s, e) => LoadSession();

            gridTabHeaderLocalSession.MouseRightButtonUp += (s, e) => FindRaceWeekends();

            RaceSessionTracker.Instance.OnRaceWeekendEnded += (s, e) => FindRaceWeekends();

            Instance = this;
        }

        private void CloseTelemetry()
        {
            comboBoxMetrics.Items.Clear();
            gridMetrics.Children.Clear();
            textBlockMetricInfo.Text = String.Empty;
            transitionContentPlots.Visibility = Visibility.Collapsed;

            Grid.SetRowSpan(gridSessionViewer, 2);

            ThreadPool.QueueUserWorkItem(x =>
            {
                Thread.Sleep(2000);
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
            });
        }

        private void FindRaceWeekends()
        {
            Dispatcher.Invoke(() =>
            {
                localRaceWeekends.Items.Clear();

                DirectoryInfo dataDir = new DirectoryInfo(FileUtil.AccManangerDataPath);
                if (!dataDir.Exists)
                    return;

                var raceWeekendFiles = new DirectoryInfo(FileUtil.AccManangerDataPath).EnumerateFiles()
                    .Where(x => !x.Name.Contains("log") && x.Extension == ".rwdb")
                    .OrderByDescending(x => x.LastWriteTimeUtc);

                foreach (FileInfo file in raceWeekendFiles)
                {
                    TextBlock textBlock = new TextBlock() { Text = file.Name.Replace(file.Extension, ""), FontSize = 12 };
                    ListViewItem lvi = new ListViewItem() { Content = textBlock, DataContext = file.FullName, Cursor = Cursors.Hand };
                    lvi.MouseLeftButtonUp += (s, e) =>
                    {
                        ListViewItem item = (ListViewItem)s;
                        OpenRaceWeekendDatabase((string)item.DataContext);
                    };
                    localRaceWeekends.Items.Add(lvi);
                }
            });
        }

        public void OpenRaceWeekendDatabase(string filename, bool focusCurrentWeekendTab = true)
        {
            if (CurrentDatabase != null)
                CurrentDatabase.Dispose(); ;

            CurrentDatabase = RaceWeekendDatabase.OpenDatabase(filename);
            if (CurrentDatabase != null)
            {
                FillTrackComboBox();
                if (focusCurrentWeekendTab)
                    tabCurrentWeekend.Focus();
            }
        }

        private void LoadSession()
        {
            DbRaceSession session = GetSelectedRaceSession();
            if (session == null) return;

            Dictionary<int, DbLapData> laps = LapDataCollection.GetForSession(CurrentDatabase, session.Id);
            stackerSessionViewer.Children.Clear();
            gridSessionLaps.Children.Clear();

            if (session == null) return;

            string sessionInfo = $"{(session.IsOnline ? "On" : "Off")}line {ACCSharedMemory.SessionTypeToString(session.SessionType)}";

            if (session.UtcEnd > session.UtcStart)
            {
                TimeSpan duration = session.UtcEnd.Subtract(session.UtcStart);
                sessionInfo += $" - Duration: {duration:hh\\:mm\\:ss}";
            }

            int potentialBestLapTime = laps.GetPotentialFastestLapTime();
            if (potentialBestLapTime != -1)
                sessionInfo += $" - Potential best: {new TimeSpan(0, 0, 0, 0, potentialBestLapTime):mm\\:ss\\:fff}";

            stackerSessionViewer.Children.Add(new TextBlock()
            {
                Text = sessionInfo,
                FontSize = 14
            });

            gridSessionLaps.Children.Add(GetLapDataGrid(laps));

            Grid.SetRowSpan(gridSessionViewer, 2);

            transitionContentPlots.Visibility = Visibility.Collapsed;
        }

        private Guid GetSelectedTrack()
        {
            if (comboTracks.SelectedIndex == -1) return Guid.Empty;
            return (Guid)(comboTracks.SelectedItem as ComboBoxItem).DataContext;
        }

        private Guid GetSelectedCar()
        {
            if (comboCars.SelectedIndex == -1) return Guid.Empty;
            return (Guid)(comboCars.SelectedItem as ComboBoxItem).DataContext;
        }

        private DbRaceSession GetSelectedRaceSession()
        {
            if (listViewRaceSessions.SelectedIndex == -1) return null;
            return (DbRaceSession)(listViewRaceSessions.SelectedItem as ListViewItem).DataContext;
        }

        public void FillCarComboBox()
        {
            if (GetSelectedTrack() == Guid.Empty)
                return;

            List<Guid> carGuidsForTrack = RaceSessionCollection.GetAllCarsForTrack(CurrentDatabase, GetSelectedTrack());
            List<DbCarData> allCars = CarDataCollection.GetAll(CurrentDatabase);

            comboCars.Items.Clear();
            foreach (DbCarData carData in allCars.Where(x => carGuidsForTrack.Contains(x.Id)))
            {
                var carModel = ConversionFactory.ParseCarName(carData.ParseName);
                string carName = ConversionFactory.GetNameFromCarModel(carModel);
                ComboBoxItem item = new ComboBoxItem() { DataContext = carData.Id, Content = carName };
                comboCars.Items.Add(item);
            }
            comboCars.SelectedIndex = 0;
        }

        public void FillTrackComboBox()
        {
            comboTracks.Items.Clear();
            List<DbTrackData> allTracks = TrackDataCollection.GetAll(CurrentDatabase);
            if (allTracks.Any())
            {
                foreach (DbTrackData track in allTracks)
                {
                    string trackName;
                    TrackNames.Tracks.TryGetValue(track.ParseName, out TrackData trackData);
                    if (trackData == null) trackName = track.ParseName;
                    else trackName = trackData.FullName;

                    ComboBoxItem item = new ComboBoxItem() { DataContext = track.Id, Content = trackName };
                    comboTracks.Items.Add(item);
                }

                comboTracks.SelectedIndex = 0;
            }
        }

        public void LoadSessionList()
        {
            List<DbRaceSession> allsessions = RaceSessionCollection.GetAll(CurrentDatabase);

            listViewRaceSessions.Items.Clear();
            var sessionsWithCorrectTrackAndCar = allsessions
                .Where(x => x.TrackId == GetSelectedTrack() && x.CarId == GetSelectedCar())
                .OrderByDescending(x => x.UtcStart);
            if (sessionsWithCorrectTrackAndCar.Any())
            {
                foreach (DbRaceSession session in sessionsWithCorrectTrackAndCar)
                {
                    DbCarData carData = CarDataCollection.GetCarData(CurrentDatabase, session.CarId);
                    DbTrackData dbTrackData = TrackDataCollection.GetTrackData(CurrentDatabase, session.TrackId);

                    var carModel = ConversionFactory.ParseCarName(carData.ParseName);
                    string carName = ConversionFactory.GetNameFromCarModel(carModel);
                    string trackName = dbTrackData.ParseName;
                    TrackNames.Tracks.TryGetValue(dbTrackData.ParseName, out TrackData trackData);
                    if (dbTrackData != null) trackName = trackData.FullName;

                    session.UtcStart = DateTime.SpecifyKind(session.UtcStart, DateTimeKind.Utc);
                    ListViewItem listItem = new ListViewItem()
                    {
                        Content = $"{ACCSharedMemory.SessionTypeToString(session.SessionType)} - {session.UtcStart.ToLocalTime():U}",
                        DataContext = session
                    };
                    listViewRaceSessions.Items.Add(listItem);
                }

                listViewRaceSessions.SelectedIndex = 0;
            }
        }

        public DataGrid GetLapDataGrid(Dictionary<int, DbLapData> laps)
        {
            var data = laps.OrderByDescending(x => x.Key).Select(x => x.Value);
            DataGrid grid = new DataGrid()
            {
                //Height = 550,
                ItemsSource = data,
                AutoGenerateColumns = false,
                CanUserDeleteRows = false,
                CanUserAddRows = false,
                IsReadOnly = true,
                EnableRowVirtualization = false,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                GridLinesVisibility = DataGridGridLinesVisibility.Vertical,
                AlternatingRowBackground = new SolidColorBrush(Color.FromArgb(25, 0, 0, 0)),
                RowBackground = Brushes.Transparent,
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            };

            int fastestLapIndex = laps.GetFastestLapIndex();
            grid.LoadingRow += (s, e) =>
            {
                DataGridRowEventArgs ev = e;
                DbLapData lapData = (DbLapData)ev.Row.DataContext;

                ev.Row.Margin = new Thickness(0);
                ev.Row.Padding = new Thickness(0);

                if (!lapData.IsValid)
                    ev.Row.Foreground = Brushes.OrangeRed;

                if (lapData.Index == fastestLapIndex)
                    ev.Row.Foreground = Brushes.LimeGreen;

                switch (lapData.LapType)
                {
                    case LapType.Outlap:
                        {
                            ev.Row.FontStyle = FontStyles.Italic;
                            break;
                        }
                    case LapType.Inlap:
                        {
                            ev.Row.FontStyle = FontStyles.Italic;
                            break;
                        }
                }

                ev.Row.PreviewMouseLeftButtonDown += (se, eve) =>
                {
                    if (ev.Row.IsSelected)
                    {
                        CloseTelemetry();
                        ev.Row.IsSelected = false;
                        eve.Handled = true;
                    }
                };
            };

            grid.Columns.Add(new DataGridTextColumn()
            {
                Header = "Lap",
                Binding = new Binding("Index"),
                SortDirection = System.ComponentModel.ListSortDirection.Descending,
                FontWeight = FontWeights.DemiBold,
            });
            grid.Columns.Add(new DataGridTextColumn()
            {
                Header = "Time",
                Binding = new Binding("Time") { Converter = new MillisecondsToFormattedTimeSpanString() }
            });
            grid.Columns.Add(new DataGridTextColumn()
            {
                Header = "Sector 1",
                Binding = new Binding("Sector1") { Converter = new DivideBy1000ToFloatConverter() }
            });
            grid.Columns.Add(new DataGridTextColumn()
            {
                Header = "Sector 2",
                Binding = new Binding("Sector2") { Converter = new DivideBy1000ToFloatConverter() }
            });
            grid.Columns.Add(new DataGridTextColumn()
            {
                Header = "Sector 3",
                Binding = new Binding("Sector3") { Converter = new DivideBy1000ToFloatConverter() }
            });
            grid.Columns.Add(new DataGridTextColumn()
            {
                Header = "Fuel Used",
                Binding = new Binding("FuelUsage") { Converter = new DivideBy1000ToFloatConverter() }
            });
            grid.Columns.Add(new DataGridTextColumn()
            {
                Header = "Fuel in tank",
                Binding = new Binding("FuelInTank")
            });
            grid.Columns.Add(new DataGridTextColumn()
            {
                Header = "Type",
                Binding = new Binding("LapType")
            });

            grid.SelectedCellsChanged += (s, e) =>
            {
                if (grid.SelectedIndex != -1)
                {
                    DbLapData lapdata = (DbLapData)grid.SelectedItem;

                    CreateCharts(lapdata.Id);
                }
            };

            return grid;
        }


        private delegate WpfPlot Plotter(Grid g, Dictionary<long, TelemetryPoint> dictio);

        private SelectionChangedEventHandler _selectionChangedHandler;
        private Dictionary<long, TelemetryPoint> _currentData;

        private void CreateCharts(Guid lapId)
        {
            //gridSessionViewer
            comboBoxMetrics.Items.Clear();
            gridMetrics.Children.Clear();
            textBlockMetricInfo.Text = String.Empty;

            DbLapTelemetry telemetry = LapTelemetryCollection.GetForLap(CurrentDatabase.GetCollection<DbLapTelemetry>(), lapId);

            if (telemetry == null)
            {
                Grid.SetRowSpan(gridSessionViewer, 2);
                transitionContentPlots.Visibility = Visibility.Collapsed;
            }
            else
            {
                Grid.SetRowSpan(gridSessionViewer, 1);
                transitionContentPlots.Visibility = Visibility.Visible;

                if (_currentData != null)
                    _currentData.Clear();

                _currentData = telemetry.DeserializeLapData();
                telemetry = null;

                TrackData trackData = TrackNames.Tracks.Values.First(x => x.Guid == GetSelectedTrack());
                int fullSteeringLock = SteeringLock.Get(CarDataCollection.GetCarData(CurrentDatabase, GetSelectedCar()).ParseName);

                Dictionary<string, Plotter> plots = new Dictionary<string, Plotter>();
                plots.Add("Inputs", (g, d) => new InputsPlot(trackData, ref textBlockMetricInfo, fullSteeringLock).Create(g, d));
                plots.Add("Speed/Gear", (g, d) => new SpeedGearPlot(trackData, ref textBlockMetricInfo).Create(g, d));
                plots.Add("Wheel Slip", (g, d) => new WheelSlipPlot(trackData, ref textBlockMetricInfo).Create(g, d));
                plots.Add("Tyre Temperatures", (g, d) => new TyreTempsPlot(trackData, ref textBlockMetricInfo).Create(g, d));
                plots.Add("Tyre Pressures", (g, d) => new TyrePressurePlot(trackData, ref textBlockMetricInfo).Create(g, d));
                plots.Add("Brake Temperatures", (g, d) => new BrakeTempsPlot(trackData, ref textBlockMetricInfo).Create(g, d));

                if (_selectionChangedHandler != null)
                {
                    comboBoxMetrics.SelectionChanged -= _selectionChangedHandler;
                    _selectionChangedHandler = null;
                }

                comboBoxMetrics.SelectionChanged += _selectionChangedHandler = new SelectionChangedEventHandler((s, e) =>
                {
                    if (comboBoxMetrics.SelectedItem == null)
                        return;

                    previousTelemetryComboSelection = comboBoxMetrics.SelectedIndex;

                    gridMetrics.Children.Clear();
                    textBlockMetricInfo.Text = String.Empty;

                    Grid grid = new Grid();
                    gridMetrics.Children.Add(grid);

                    Plotter plotter = (Plotter)(comboBoxMetrics.SelectedItem as ComboBoxItem).DataContext;
                    grid.Children.Add(plotter.Invoke(grid, _currentData));

                    ThreadPool.QueueUserWorkItem(x =>
                    {
                        Thread.Sleep(2000);
                        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                    });
                });

                foreach (var plot in plots)
                {
                    ComboBoxItem boxItem = new ComboBoxItem()
                    {
                        Content = plot.Key,
                        DataContext = plot.Value
                    };
                    comboBoxMetrics.Items.Add(boxItem);
                }

                if (comboBoxMetrics.Items.Count > 0)
                {
                    int toSelect = previousTelemetryComboSelection;
                    if (toSelect == -1) toSelect = 0;
                    comboBoxMetrics.SelectedIndex = toSelect;
                }
            }
        }
    }
}
