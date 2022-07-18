﻿using ACC_Manager.Util.SystemExtensions;
using ACCManager.Broadcast;
using ACCManager.Broadcast.Structs;
using ACCManager.Data;
using ACCManager.Data.ACC.EntryList;
using ACCManager.Data.ACC.EntryList.TrackPositionGraph;
using ACCManager.Data.ACC.Session;
using ACCManager.Data.ACC.Tracker;
using ACCManager.HUD.Overlay.Configuration;
using ACCManager.HUD.Overlay.Internal;
using ACCManager.HUD.Overlay.OverlayUtil;
using ACCManager.HUD.Overlay.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using static ACCManager.ACCSharedMemory;
using static ACCManager.Data.ACC.EntryList.EntryListTracker;
using static ACCManager.Data.SetupConverter;

namespace ACCManager.HUD.ACC.Overlays.OverlayStandings
{
#if DEBUG
    [Overlay(Name = "Live Standings", Version = 1.00,
    Description = "Shows standings table during the race for different car classes.", OverlayType = OverlayType.Debug)]
#endif

    public partial class StandingsOverlay : AbstractOverlay
    {
        private StandingsConfiguration _config = new StandingsConfiguration();

        private class StandingsConfiguration : OverlayConfiguration
        {
            public StandingsConfiguration()
            {
                this.AllowRescale = true;
            }

            [ToolTip("Shows driver class only.")]
            internal bool ShowDriverClassOnly { get; set; } = false;

            [ToolTip("Number of cars in front and behind me.")]
            [IntRange(1, 5, 1)]
            public int PlacesAroundMyCar { get; set; } = 2;

            [ToolTip("Number of rows in other car classes standings table.")]
            [IntRange(1, 10, 1)]
            public int NumberOfCarsOnStandingsTable { get; set; } = 4;

        }

        private const int _height = 800;
        private const int _width = 800;
        private float _trackMeter = 0;

        private CarClasses _ownClass = CarClasses.GT3;
        private String _driverLastName = "";
        private bool _initDone = false;

        // the entry list splint into separate lists for every car class
        private Dictionary<CarClasses, List<KeyValuePair<int, CarData>>> _entryListForCarClass = new Dictionary<CarClasses, List<KeyValuePair<int, CarData>>>();

        private Dictionary<CarClasses, SolidBrush> _carClassToBrush = new Dictionary<CarClasses, SolidBrush>()
        {
            {CarClasses.GT3, new SolidBrush(Color.FromArgb(150, Color.Yellow))},
            {CarClasses.GT4, new SolidBrush(Color.FromArgb(150, Color.LightBlue))},
            {CarClasses.CUP, new SolidBrush(Color.FromArgb(150, Color.Cyan))},
            {CarClasses.ST, new SolidBrush(Color.FromArgb(150, Color.DarkGoldenrod))},
            {CarClasses.TCX, new SolidBrush(Color.FromArgb(150, Color.DarkRed))},
            {CarClasses.CHL, new SolidBrush(Color.FromArgb(150, Color.DarkBlue))},

        };

        public StandingsOverlay(Rectangle rectangle) : base(rectangle, "Standings Overlay")
        {
            this.Height = _height;
            this.Width = _width;
            this.RefreshRateHz = 10;
            InitCarClassEntryLists();
        }

        public override void BeforeStart()
        {
            RaceSessionTracker.Instance.OnACStatusChanged += StatusChanged;
            RaceSessionTracker.Instance.OnACSessionTypeChanged += SessionTypeChanged;
            BroadcastTracker.Instance.OnTrackDataUpdate += TrackDataUpdate;
        }

        public override void BeforeStop()
        {
            RaceSessionTracker.Instance.OnACStatusChanged -= StatusChanged;
            RaceSessionTracker.Instance.OnACSessionTypeChanged -= SessionTypeChanged;
            BroadcastTracker.Instance.OnTrackDataUpdate -= TrackDataUpdate;
            
        }

        private void TrackDataUpdate(object sender, TrackData e)
        {
            _trackMeter = e.TrackMeters;
        }

        private void SessionTypeChanged(object sender, ACCSharedMemory.AcSessionType e)
        {
            _initDone = false;
            ClearCarClassEntryList();
        }

        private void StatusChanged(object sender, ACCSharedMemory.AcStatus e)
        {
            if (e.Equals(AcStatus.AC_OFF))
            {
                _initDone = false;
                ClearCarClassEntryList();
            }
        }

        private void ClearCarClassEntryList()
        {
            foreach (CarClasses carClass in Enum.GetValues(typeof(CarClasses)))
            {
                _entryListForCarClass[carClass].Clear();
            }
        }

        private void InitCarClassEntryLists()
        {
            _entryListForCarClass.Clear();
            foreach (CarClasses carClass in Enum.GetValues(typeof(CarClasses)))
            {
                _entryListForCarClass[carClass] = new List<KeyValuePair<int, CarData>>();
            }
        }

        public override void Render(Graphics g)
        {

            List<KeyValuePair<int, CarData>> cars = EntryListTracker.Instance.Cars;
            if (cars.Count == 0) return;

            DetermineOwnClass(cars);
            SplitEntryList(cars);
            SortAllEntryLists();

            if (!_initDone) return;

            int bestSessionLapMS = GetBestSessionLap();
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Dictionary<CarClasses, List<StandingsTableRow>> tableRows = new Dictionary<CarClasses, List<StandingsTableRow>>();

            foreach (CarClasses carClass in Enum.GetValues(typeof(CarClasses)))
            {
                tableRows[carClass] = new List<StandingsTableRow>();

                if (carClass == _ownClass)
                {
                    int playersIndex = 0;

                    for (int i = 0; i < _entryListForCarClass[carClass].Count(); i++)
                    {
                        CarData carData = _entryListForCarClass[carClass][i].Value;
                        
                        if (pageGraphics.PlayerCarID == carData.CarInfo.CarIndex)
                        {
                            playersIndex = i;
                            break;
                        }
                    }

                    int startIdx = (playersIndex - _config.PlacesAroundMyCar) < 0 ? 0 : (playersIndex - _config.PlacesAroundMyCar);
                    int endIdx = (playersIndex + _config.PlacesAroundMyCar + 1) > _entryListForCarClass[carClass].Count() ? _entryListForCarClass[carClass].Count() : (playersIndex + _config.PlacesAroundMyCar + 1);

                    for (int i = startIdx; i < endIdx; i++)
                    {
                        CarData carData = _entryListForCarClass[carClass][i].Value;

                        var gab = GetGabToCarInFront(_entryListForCarClass[carClass], i);
                        //AddCarDataTableRow(carData, tableRows[carClass], (carData.RealtimeCarUpdate.LastLap.LaptimeMS == bestSessionLapMS));
                        AddCarDataTableRow(carData, tableRows[carClass], gab, false);
                    }
                }
                else if (!_config.ShowDriverClassOnly)
                {
                    for (int i = 0; i < (_entryListForCarClass[carClass].Count() < _config.NumberOfCarsOnStandingsTable ? _entryListForCarClass[carClass].Count() : _config.NumberOfCarsOnStandingsTable); i++)
                    {
                        CarData carData = _entryListForCarClass[carClass][i].Value;
                        //AddCarDataTableRow(carData, tableRows[carClass], (carData.RealtimeCarUpdate.LastLap.LaptimeMS == bestSessionLapMS));
                        var gab = GetGabToCarInFront(_entryListForCarClass[carClass], i);
                        AddCarDataTableRow(carData, tableRows[carClass], gab, false);
                    }
                }
            }

            OverlayStandingsTable ost = new OverlayStandingsTable(10, 10, 11);

            int height = 0;
            foreach (KeyValuePair<CarClasses, List<StandingsTableRow>> kvp in tableRows)
            {
                ost.Draw(g, height, kvp.Value, _carClassToBrush[kvp.Key], kvp.Key.ToString() + " / " + _entryListForCarClass[kvp.Key].Count() + " Cars", _driverLastName);
                height = ost.Height;
            }

        }

        private string GetGabToCarInFront(List<KeyValuePair<int, CarData>> list, int i)
        {
            // inspired by acc bradcasting client

            if (i < 1 || _trackMeter == 0) return "---";

            var carInFront = list[i - 1].Value.RealtimeCarUpdate;
            var currentCar = list[i].Value.RealtimeCarUpdate;
            var splineDistance = Math.Abs(carInFront.SplinePosition - currentCar.SplinePosition);
            while (splineDistance > 1f)
                splineDistance -= 1f;
            var gabMeters = splineDistance * _trackMeter;

            if (currentCar.Kmh < 10)
            {
                return "---";
            }
            return $"{gabMeters / currentCar.Kmh * 3.6:F1}s ⇅";

        }

        private int GetBestSessionLap()
        {
            int bestSessionLapMS = -1;
            if (broadCastRealTime.BestSessionLap != null)
            {
                bestSessionLapMS = broadCastRealTime.BestSessionLap.LaptimeMS.GetValueOrDefault(-1);
            }
            return bestSessionLapMS;
        }

        private void AddCarDataTableRow(CarData carData, List<StandingsTableRow> standingsTableRows, String gab, bool fastestLapTime)
        {
            DriverInfo driverInfo = carData.CarInfo.Drivers[carData.CarInfo.CurrentDriverIndex];
            string firstName = driverInfo.FirstName;
            if (firstName.Length > 0) firstName = firstName.First() + "";
            string diverName = $"{firstName}. {driverInfo.LastName}";
            int cupPosition = carData.RealtimeCarUpdate.CupPosition;

            String lapTime = GetLastLapTime(carData.RealtimeCarUpdate);
            String additionInfo = AdditionalInfo(carData.RealtimeCarUpdate);

            standingsTableRows.Add(new StandingsTableRow()
            {
                Position = cupPosition,
                RaceNumber = carData.CarInfo.RaceNumber,
                DriverName = diverName,
                LapTime = lapTime,
                Delta = carData.RealtimeCarUpdate.Delta,
                Gab = gab,
                AdditionalInfo = additionInfo,
                FastestLapTime = fastestLapTime
            });
        }

        private String AdditionalInfo(RealtimeCarUpdate realtimeCarUpdate)
        {
            if (broadCastRealTime.SessionType != RaceSessionType.Race) return "";

            switch (realtimeCarUpdate.CarLocation)
            {
                case CarLocationEnum.PitEntry:
                    {
                        return "PIT Entry";
                    }
                case CarLocationEnum.PitExit:
                    {
                        return "PIT Exit";
                    }
                case CarLocationEnum.Pitlane:
                    {
                        return "Box";
                    }
                default:
                    return "";
            }
        }

        private String GetLastLapTime(RealtimeCarUpdate realtimeCarUpdate)
        {
            if (realtimeCarUpdate.LastLap == null || !realtimeCarUpdate.LastLap.LaptimeMS.HasValue)
            {
                return "--:--.---";
            }

            TimeSpan lapTime = TimeSpan.FromMilliseconds(realtimeCarUpdate.LastLap.LaptimeMS.Value);
            return $"{lapTime:mm\\:ss\\.fff}";
        }

        private void SortAllEntryLists()
        {
            foreach (CarClasses carClass in Enum.GetValues(typeof(CarClasses)))
            {
                SortEntryList(_entryListForCarClass[carClass]);
            }
        }

        private void SortEntryList(List<KeyValuePair<int, CarData>> cars)
        {
            if (cars.Count == 0) return;

            switch (broadCastRealTime.SessionType)
            {
                case RaceSessionType.Practice:
                case RaceSessionType.Race:
                    {
                        switch (broadCastRealTime.Phase)
                        {
                            case SessionPhase.SessionOver:
                            case SessionPhase.PreSession:
                            case SessionPhase.PreFormation:
                                {
                                    cars.Sort((a, b) =>
                                    {
                                        return a.Value.RealtimeCarUpdate.CupPosition.CompareTo(b.Value.RealtimeCarUpdate.CupPosition);
                                    });
                                    break;
                                }
                            default:
                                {
                                    cars.Sort((a, b) =>
                                    {
                                        if (a.Value.CarInfo == null)
                                            return -1;

                                        if (b.Value.CarInfo == null)
                                            return 1;

                                        Car carCarA = PositionGraph.Instance.GetCar(a.Value.CarInfo.CarIndex);
                                        Car carCarb = PositionGraph.Instance.GetCar(b.Value.CarInfo.CarIndex);

                                        if (carCarA == null) return -1;
                                        if (carCarb == null) return 1;

                                        var aSpline = carCarA.SplinePosition;
                                        var bSpline = carCarb.SplinePosition;

                                        var aLaps = carCarA.LapIndex;
                                        var bLaps = carCarb.LapIndex;

                                        float aPosition = aLaps + aSpline / 10;
                                        float bPosition = bLaps + bSpline / 10;
                                        return aPosition.CompareTo(bPosition);
                                    });
                                    cars.Reverse();
                                    break;
                                };
                        }
                        break;
                    }


                case RaceSessionType.Qualifying:
                    {
                        cars.Sort((a, b) =>
                        {
                            return a.Value.RealtimeCarUpdate.CupPosition.CompareTo(b.Value.RealtimeCarUpdate.CupPosition);
                        });
                        break;
                    }

                default: break;
            }
        }

        private void DetermineOwnClass(List<KeyValuePair<int, CarData>> cars)
        {

            if (_initDone) return;

            foreach (KeyValuePair<int, CarData> kvp in cars)
            {
                if (kvp.Key == pageGraphics.PlayerCarID)
                {
                    var carModel = ConversionFactory.GetCarModels(kvp.Value.CarInfo.CarModelType);
                    _ownClass = ConversionFactory.GetConversion(carModel).CarClass;

                    //DriverInfo driverInfo = kvp.Value.CarInfo.Drivers[kvp.Value.CarInfo.CurrentDriverIndex];

                    DriverInfo driverInfo = kvp.Value.CarInfo.Drivers[kvp.Value.CarInfo.CurrentDriverIndex];
                    string firstName = driverInfo.FirstName;
                    if (firstName.Length > 0) firstName = firstName.First() + "";
                    _driverLastName = $"{firstName}. {driverInfo.LastName}";

                    //_driverLastName = driverInfo.LastName;
                    Debug.WriteLine($"standings overlay - car class {_ownClass} driver name {_driverLastName}");
                    _initDone = true;

                }
            }
        }

        private void SplitEntryList(List<KeyValuePair<int, CarData>> cars)
        {
            ClearCarClassEntryList();

            foreach (KeyValuePair<int, CarData> kvp in cars)
            {
                if (kvp.Value.CarInfo == null)
                {
                    return;
                }

                var carModel = ConversionFactory.GetCarModels(kvp.Value.CarInfo.CarModelType);
                var carClass = ConversionFactory.GetConversion(carModel).CarClass;

                switch (carClass)
                {
                    case CarClasses.GT3:
                        _entryListForCarClass[CarClasses.GT3].Add(kvp);
                        break;
                    case CarClasses.GT4:
                        _entryListForCarClass[CarClasses.GT4].Add(kvp);
                        break;
                    case CarClasses.CUP:
                        _entryListForCarClass[CarClasses.CUP].Add(kvp);
                        break;
                    case CarClasses.ST:
                        _entryListForCarClass[CarClasses.ST].Add(kvp);
                        break;
                    case CarClasses.TCX:
                        _entryListForCarClass[CarClasses.TCX].Add(kvp);
                        break;
                    case CarClasses.CHL:
                        _entryListForCarClass[CarClasses.CHL].Add(kvp);
                        break;
                    default:
                        break;
                }
            }

        }

        public override bool ShouldRender()
        {
            return DefaultShouldRender();
        }

    }

    public class StandingsTableRow
    {
        public int Position { get; set; }
        public int RaceNumber { get; set; }
        public String DriverName { get; set; }
        public String LapTime { get; set; }
        public int Delta { get; set; }
        public String Gab { get; set; }
        public bool FastestLapTime { get; set; }
        public String AdditionalInfo { get; set; }
    }

    public class OverlayStandingsTable
    {
        public int Height { get; set; }
        private readonly int _x;
        private readonly int _y;
        private readonly int _columnGab = 5;
        private readonly int _rowGab = 3;
        private readonly int _fontSize = 0;

        private readonly SolidBrush _oddBackground = new SolidBrush(Color.FromArgb(100, Color.Black));
        private readonly SolidBrush _evenBackground = new SolidBrush(Color.FromArgb(180, Color.Black));
        private readonly SolidBrush _ownCarBackground = new SolidBrush(Color.FromArgb(180, Color.DarkSeaGreen));
        //Color.FromArgb(120, Color.Red);

        public OverlayStandingsTable(int x, int y, int fontSize)
        {
            _x = x;
            _y = y;
            _fontSize = fontSize;
        }

        public void Draw(Graphics g, int y, List<StandingsTableRow> tableData, SolidBrush classBackground, string header, string ownName)
        {
            var rowPosY = _y + y;

            if (tableData.Count == 0) return;

            OverlayStandingTableHeaderLabel tableHeader = new OverlayStandingTableHeaderLabel(g, _x, rowPosY, FontUtil.FontUnispace(_fontSize));
            tableHeader.Draw(g, classBackground, Brushes.White, header);
            rowPosY += tableHeader.Height + _rowGab;

            for (int i = 0; i < tableData.Count; i++)
            {

                var columnPosX = _x;

                SolidBrush backgroundColor = _oddBackground;
                if (i % 2 == 0)
                {
                    backgroundColor = _evenBackground;
                }

                if (tableData[i].DriverName.Equals(ownName))
                {
                    backgroundColor = _ownCarBackground;
                }

                //String deltaString = $"{tableData[i].Delta / 1000f:F2}".FillStart(6, ' ');
                String deltaString = $"{TimeSpan.FromMilliseconds(tableData[i].Delta):ss\\.f}";
                
                OverlayStandingsTablePositionLabel position = new OverlayStandingsTablePositionLabel(g, columnPosX, rowPosY, FontUtil.FontUnispace(_fontSize));
                position.Draw(g, backgroundColor, classBackground, tableData[i].Position.ToString());

                columnPosX += position.Width + _columnGab;
                OverlayStandingsTableTextLabel raceNumber = new OverlayStandingsTableTextLabel(g, columnPosX, rowPosY, 4, FontUtil.FontUnispace(_fontSize));
                raceNumber.Draw(g, backgroundColor, (SolidBrush)Brushes.White, Brushes.White, "#" + tableData[i].RaceNumber.ToString(), false);

                columnPosX += raceNumber.Width + _columnGab;
                OverlayStandingsTableTextLabel driverName = new OverlayStandingsTableTextLabel(g, columnPosX, rowPosY, 20, FontUtil.FontUnispace(_fontSize));
                driverName.Draw(g, backgroundColor, (SolidBrush)Brushes.Purple, Brushes.White, tableData[i].DriverName, false);

                columnPosX += driverName.Width + _columnGab;
                OverlayStandingsTableTextLabel deltaTime = new OverlayStandingsTableTextLabel(g, columnPosX, rowPosY, 4, FontUtil.FontUnispace(_fontSize));
                deltaTime.Draw(g, backgroundColor, (SolidBrush)Brushes.Green, Brushes.White, deltaString, (tableData[i].Delta < -100));

                columnPosX += deltaTime.Width + _columnGab;
                OverlayStandingsTableTextLabel gabTime = new OverlayStandingsTableTextLabel(g, columnPosX, rowPosY, 3, FontUtil.FontUnispace(_fontSize));
                gabTime.Draw(g, backgroundColor, (SolidBrush)Brushes.Green, Brushes.White, tableData[i].Gab, false);

                columnPosX += gabTime.Width + _columnGab;
                OverlayStandingsTableTextLabel laptTime = new OverlayStandingsTableTextLabel(g, columnPosX, rowPosY, 9, FontUtil.FontUnispace(_fontSize));
                laptTime.Draw(g, backgroundColor, (SolidBrush)Brushes.Purple, Brushes.White, tableData[i].LapTime, tableData[i].FastestLapTime);

                if (tableData[i].AdditionalInfo != "")
                {
                    laptTime.DrawAdditionalInfo(g, tableData[i].AdditionalInfo);
                }

                rowPosY += position.Height + _rowGab;

            }
            Height = rowPosY;
        }
    }

    public class OverlayStandingTableHeaderLabel
    {
        private readonly int _x;
        private readonly int _y;
        public int Height { get; }
        private readonly Font _fontFamily;

        public OverlayStandingTableHeaderLabel(Graphics g, int x, int y, Font font)
        {
            _x = x;
            _y = y;
            _fontFamily = font;
            var fontSize = g.MeasureString(" ", _fontFamily);
            Height = (int)fontSize.Height;
        }

        public void Draw(Graphics g, SolidBrush backgroundBrush, Brush fontBruch, String text)
        {
            var fontSize = g.MeasureString(text, _fontFamily);
            var rectanle = new Rectangle(_x, _y, (int)(fontSize.Width + 10), (int)fontSize.Height);
            g.FillRoundedRectangle(backgroundBrush, rectanle, 3);
            g.DrawStringWithShadow(text, _fontFamily, fontBruch, new PointF(_x, _y));
        }
    }

    public class OverlayStandingsTableTextLabel
    {
        private readonly int _x;
        private readonly int _y;
        private readonly int _height;
        private readonly int _maxStringLength;
        private readonly Font _fontFamily;

        private readonly int _spacing = 10; // possible to set value in contructor

        public int Width { get; }


        public OverlayStandingsTableTextLabel(Graphics g, int x, int y, int maxStringLength, Font font)
        {
            _fontFamily = font;
            _maxStringLength = maxStringLength;
            _x = x;
            _y = y;

            string maxString = new string('K', _maxStringLength);
            var fontSize = g.MeasureString(maxString, _fontFamily);
            Width = (int)fontSize.Width + _spacing;
            _height = (int)fontSize.Height;

        }

        public void Draw(Graphics g, SolidBrush backgroundBrush, SolidBrush highlightBrush, Brush fontBruch, String text, bool highlight)
        {
            Rectangle graphRect = new Rectangle(_x-(_spacing/2), _y, (int)Width, (int)_height);

            if (highlight)
            {
                LinearGradientBrush lgb = new LinearGradientBrush(graphRect, backgroundBrush.Color, highlightBrush.Color, 0f, true);
                lgb.SetBlendTriangularShape(.5f, .6f);
                g.FillRectangle(lgb, graphRect);
                Rectangle hightlightRect = new Rectangle(_x, _y + (int)_height - 4, (int)Width, 4);
                g.FillRoundedRectangle(highlightBrush, hightlightRect, 3);
            }
            else
            {
                g.FillRoundedRectangle(backgroundBrush, graphRect, 3);
            }

            g.DrawStringWithShadow(TruncateString(text), _fontFamily, fontBruch, new PointF(_x, _y));

        }

        public void DrawAdditionalInfo(Graphics g, String text)
        {
            var fontSize = g.MeasureString(text, _fontFamily);

            var rectanle = new Rectangle(_x + Width, _y, (int)(fontSize.Width + 10), _height);
            var path = GraphicsExtensions.CreateRoundedRectangle(rectanle, 0, _height / 4, _height / 4, 0);
            g.FillPath(Brushes.DarkGreen, path);
            g.DrawString(text, _fontFamily, Brushes.White, new PointF(_x + (int)(Width + 5), _y));

        }

        private string TruncateString(String text)
        {
            return text.Length <= _maxStringLength ? text : text.Substring(0, _maxStringLength);
        }
    }


    public class OverlayStandingsTablePositionLabel
    {
        private readonly Font _fontFamily;

        private readonly int _x;
        private readonly int _y;
        public int Width { get; }
        public int Height { get; }
        private readonly int _maxFontWidth;

        private readonly GraphicsPath _path;

        public OverlayStandingsTablePositionLabel(Graphics g, int x, int y, Font font)
        {

            _x = x;
            _y = y;
            _fontFamily = font;

            string maxString = new string('K', 2); // max two figures are allowed :-)
            var fontSize = g.MeasureString(maxString, _fontFamily);
            _maxFontWidth = (int)fontSize.Width;
            Width = (int)(fontSize.Width);
            Height = (int)(fontSize.Height);


            var rectanle = new Rectangle(_x, _y, Width, Height);
            _path = GraphicsExtensions.CreateRoundedRectangle(rectanle, 0, 0, Height / 3, 0);

        }

        public void Draw(Graphics g, SolidBrush background, SolidBrush highlight, String number)
        {

            LinearGradientBrush lgb = new LinearGradientBrush(new Point() { X = _x - 10, Y = _y }, new Point() { X = Width + 10, Y = _y }, highlight.Color, background.Color);
            g.FillPath(lgb, _path);

            var rectanle = new Rectangle(_x, _y, 4, Height);
            g.FillRectangle(highlight, rectanle);

            var numberWidth = g.MeasureString(number, _fontFamily).Width;
            g.DrawString(number, _fontFamily, Brushes.White, new PointF(_x + _maxFontWidth / 2 - numberWidth / 2, _y));

        }
    }
}
