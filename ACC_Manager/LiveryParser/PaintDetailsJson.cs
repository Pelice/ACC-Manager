﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACCManager.LiveryParser
{
    // (Sponsors.json + Decals.json)
    public class PaintDetailsJson
    {
        public class Root
        {
            public int BaseRoughness { get; set; }
            public int ClearCoat { get; set; }
            public int ClearCoatRoughness { get; set; }
            public int Metallic { get; set; }
        }
    }
}
