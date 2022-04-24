﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACCSetupApp.Controls
{
    internal class InputGraph
    {
        private int X, Y;
        private int Width, Height;
        private LinkedList<int> ThrottleData;
        private LinkedList<int> BrakeData;
        private LinkedList<int> SteeringData;

        public InputGraph(int x, int y, int width, int height, LinkedList<int> throttleData, LinkedList<int> brakeData, LinkedList<int> steeringData)
        {
            this.X = x;
            this.Y = y;
            this.Width = width;
            this.Height = height;
            this.SteeringData = steeringData;
            this.ThrottleData = throttleData;
            this.BrakeData = brakeData;
        }

        private int getRelativeNodeY(int value)
        {
            double range = 100 - 0;
            double percentage = 1d - (value - 0) / range;
            return (int)(percentage * (Height - Height / 5))
                    + Height / 10;
        }

        public void Draw(Graphics g)
        {
            Rectangle graphRect = new Rectangle(X, Y, Width, Height);
            // draw background
            g.FillRectangle(new SolidBrush(Color.FromArgb(196, Color.Black)), graphRect);

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            DrawData(g, BrakeData, Brushes.Red);
            DrawData(g, ThrottleData, Brushes.ForestGreen);
            DrawData(g, SteeringData, Brushes.White);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;

            g.DrawRectangle(new Pen(Color.FromArgb(196, Color.Black)), graphRect);
        }

        private void DrawData(Graphics g, LinkedList<int> Data, Brush color)
        {
            if (Data.Count > 0)
            {
                List<Point> points = new List<Point>();
                lock (Data)
                    for (int i = 0; i < Data.Count - 1; i++)
                    {
                        int x = X + Width - i * (Width / Data.Count);
                        lock (Data)
                        {
                            int y = Y + getRelativeNodeY(Data.ElementAt(i));
                            if (y > Y + Height)
                            {
                                y = Y + Height;
                            }
                            if (x < X)
                                break;

                            points.Add(new Point(x, y));
                        }

                    }

                if (points.Count > 0)
                {
                    GraphicsPath path = new GraphicsPath();
                    path.AddLines(points.ToArray());
                    g.DrawPath(new Pen(color, 1.9f), path);
                }
            }
        }
    }
}