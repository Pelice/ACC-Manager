﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACCManager.HUD.Overlay.Configuration
{
    public class IntRangeAttribute : Attribute
    {
        public int Min;
        public int Max;
        public int Increment;

        public IntRangeAttribute(int min, int max, int increment)
        {
            Min = min;
            Max = max;
            Increment = increment;
        }

        public static int[] GetOptionsCollection(IntRangeAttribute intRange)
        {
            List<int> collection = new List<int>();

            for (int i = intRange.Min; i < intRange.Max + intRange.Increment; i += intRange.Increment)
            {
                collection.Add(i);
            }

            return collection.ToArray();
        }
    }
}
