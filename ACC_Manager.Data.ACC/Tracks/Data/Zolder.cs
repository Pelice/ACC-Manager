﻿using ACC_Manager.Util.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RaceElement.Data.ACC.Tracks.TrackData;

namespace ACCManager.Data.ACC.Tracks.Data
{
    internal class Zolder : AbstractTrackData
    {
        public override Guid Guid => new Guid("eaca1a4d-aa7e-4c31-bfc5-6035bfa30395");
        public override string FullName => "Circuit Zolder";
        public override int TrackLength => 4011;

        public override Dictionary<FloatRangeStruct, string> CornerNames => new Dictionary<FloatRangeStruct, string>()        
        {
            { new FloatRangeStruct(0.04245049f, 0.0946003f), "Earste Links" },
            { new FloatRangeStruct(0.1296046f, 0.1782782f), "Sterrenwachtbocht" },
            { new FloatRangeStruct(0.17827821f, 0.2351831f), "Kanaalbocht" },
            { new FloatRangeStruct(0.2595046f, 0.3127186f), "Lucien Bianchibocht" },
            { new FloatRangeStruct(0.4122366f, 0.4614663f), "Kleine Chicane" },
            { new FloatRangeStruct(0.5095074f, 0.5512679f), "Butte" },
            { new FloatRangeStruct(0.5667552f, 0.6171156f), "Gille Villeneuve Chicane" },
            { new FloatRangeStruct(0.6240816f, 0.6695773f), "Terlamenbocht" },
            { new FloatRangeStruct(0.7329645f, 0.7745231f), "Bolderberghaarspeldbocht" },
            { new FloatRangeStruct(0.7820684f, 0.8041771f), "Jochen Rindtbocht" },
            { new FloatRangeStruct(0.8749502f, 0.9376088f), "Jackie Ickxbocht" }
        };
    }
}
