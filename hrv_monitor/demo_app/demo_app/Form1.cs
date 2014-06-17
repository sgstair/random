/*
Copyright (c) 2014 Stephen Stair (sgstair@akkit.org)

Permission is hereby granted, free of charge, to any person obtaining a copy
 of this software and associated documentation files (the "Software"), to deal
 in the Software without restriction, including without limitation the rights
 to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
 all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace demo_app
{
    public partial class Form1 : Form
    {
        hrv_monitor Device;

        public Form1()
        {
            InitializeComponent();

            this.Text = "hrv_monitor Demo App";

            this.DoubleBuffered = true;

            this.Paint += Form1_Paint;
            this.Scroll += Form1_Scroll;
            this.Resize += Form1_Resize;
            this.MouseWheel += Form1_MouseWheel;


            SamplesPerPixel = 3;
            FollowingLeftEdge = true;

            hrv_monitor_instance[] instances = hrv_monitor.EnumerateInstances();
            if(instances.Length > 0)
            {
                Device = new hrv_monitor(instances[0]);
                Device.Data.HaveNewData += Data_HaveNewData;
            }
        }

        void Form1_MouseWheel(object sender, MouseEventArgs e)
        {
            
        }

        void Form1_Resize(object sender, EventArgs e)
        {
            this.Invalidate();
        }

        void Data_HaveNewData()
        {
            lock(this)
            {
                // Trim, but not when rendering.
                int removed = Device.Data.TrimEarlySamples(1000000);

                this.Invalidate(); // Redraw.
            }
        }

        void Form1_Scroll(object sender, ScrollEventArgs e)
        {
            
        }

        bool FollowingLeftEdge; // Whether we are locked to watching the most recent data
        int FollowingSample; // The last sample to display on the very left edge of the window.

        double SamplesPerPixel;
        const int BigFontPixelHeight = 40;
        Font BigFont = new Font(FontFamily.GenericSansSerif, BigFontPixelHeight);

        void RenderOverlay(Graphics g, string text)
        {
            SizeF size = g.MeasureString(text, BigFont);
            g.DrawString(text, BigFont, Brushes.Black,
                (ClientRectangle.Width - size.Width) / 2,
                (ClientRectangle.Height - size.Height) / 2);

        }

        void Form1_Paint(object sender, PaintEventArgs e)
        {
            const int ConjunctionPixels = 2;
            Color BackgroundColor = Color.LightGray;
            Pen GridLines = Pens.DarkBlue;
            Brush ConjunctionColor = Brushes.DarkRed;
            Pen DarkTrace = Pens.Black;
            Pen LED1Trace = Pens.Red;
            Pen LED2Trace = Pens.Green;
            Pen LED3Trace = Pens.Blue;
            Pen LED4Trace = Pens.Magenta;

            lock (this)
            {
                Graphics g = e.Graphics;
                if (Device != null)
                {
                    Device.Data.Freeze();
                    try
                    {
                        int RenderWidth = ClientRectangle.Width;
                        int RenderHeight = ClientRectangle.Height;

                        if(Device.Data.Spans == 0)
                        {
                            g.Clear(BackgroundColor);
                            RenderOverlay(g, "No Data");
                            return;
                        }

                        if(FollowingLeftEdge)
                        {
                            FollowingSample = Device.Data.TotalSamples-1;
                        }

                        // Map out the sizes of all the chunks and connections
                        int[] SpanStart = new int[Device.Data.Spans];
                        int cursor = 0;
                        for (int i = 0; i < Device.Data.Spans; i++)
                        {
                            SpanStart[i] = cursor;
                            cursor += Device.Data[i].Count;
                        }

                        // Determine range of data to draw and locations.
                        int pixels = this.ClientRectangle.Width;
                        int StartSpan = Device.Data.Spans - 1;
                        while (SpanStart[StartSpan] > FollowingSample)
                        {
                            StartSpan--;
                        }

                        // Start at the left of the screen and work our way back until we cover all of them
                        // We're allowing for the subpixels to skew around a bit to simplify the algorithm.
                        // A span will start at a fixed integer pixel, and will consist of all all of the
                        //   samples from the offset to the end of the span, or the end of the screen.

                        int[] SpanPixelStart = new int[Device.Data.Spans]; // First pixels of span are the conjunction.
                        int[] SpanSampleStart = new int[Device.Data.Spans];
                        cursor = RenderWidth;
                        int cursorSample = FollowingSample;
                        for (int i = StartSpan; i>=0; i--)
                        {
                            int numSamples = cursorSample - SpanStart[i] + 1;
                            int numPixels = (int)Math.Ceiling(numSamples / SamplesPerPixel);
                            SpanSampleStart[i] = SpanStart[i];
                            if(numPixels > cursor)
                            {
                                numSamples = (int)Math.Floor(cursor * SamplesPerPixel);
                                SpanSampleStart[i] = cursorSample - numSamples + 1;
                                numPixels = cursor;
                            }
                            cursor -= numPixels - ConjunctionPixels;
                            SpanPixelStart[i] = cursor;
                            cursorSample = SpanStart[i]-1;
                        }


                        // Draw the background
                        g.Clear(BackgroundColor);

                        // Draw the conjunctions between spans
                        for (int i = StartSpan; i >= 0; i--)
                        {
                            if(SpanSampleStart[i] == 0) { break; }
                            g.FillRectangle(ConjunctionColor,SpanPixelStart[i],0,ConjunctionPixels,RenderHeight);
                        }

                        // Draw out the spans of data in view

                        cursor = ClientRectangle.Width;
                        int sampleEnd = FollowingSample;
                        for (int i = StartSpan; i >= 0; i--)
                        {
                            if (cursor < 0) break;
                            int pixelStart = SpanPixelStart[i] + ConjunctionPixels;
                            int sampleStart = SpanSampleStart[i];

                            if (sampleStart >= sampleEnd) continue;

                            DrawLineSegment(g, DarkTrace, pixelStart, sampleStart, sampleEnd, RenderHeight, (n) => Device.Data[i][n - SpanStart[i]].DarkValue);

                            DrawLineSegment(g, LED1Trace, pixelStart, sampleStart, sampleEnd, RenderHeight, (n) => Device.Data[i][n - SpanStart[i]].LED1Value);
                            DrawLineSegment(g, LED2Trace, pixelStart, sampleStart, sampleEnd, RenderHeight, (n) => Device.Data[i][n - SpanStart[i]].LED2Value);
                            DrawLineSegment(g, LED3Trace, pixelStart, sampleStart, sampleEnd, RenderHeight, (n) => Device.Data[i][n - SpanStart[i]].LED3Value);
                            DrawLineSegment(g, LED4Trace, pixelStart, sampleStart, sampleEnd, RenderHeight, (n) => Device.Data[i][n - SpanStart[i]].LED4Value);


                            sampleEnd = SpanStart[i] - 1;
                            cursor = pixelStart-1;
                        }

                    }
                    finally
                    {
                        Device.Data.Thaw();
                    }
                }
                else
                {
                    g.Clear(BackgroundColor);
                    RenderOverlay(g, "No Device");
                }
            }
        }

        void DrawLineSegment(Graphics g, Pen p, float x, int start, int end, int renderHeight, Func<int,int> value)
        {
            int lastValue = value(start);
            int curValue;
            float x1, x2;
            float y1, y2;
            x1 = x;
            for (int n = start + 1; n <= end; n++)
            {
                x2 = x + (float)((n - start) / SamplesPerPixel);
                curValue = value(n);

                // Range of values is 0 - 0x3fff, translate this to the window bounds.
                y1 = (1.0f - lastValue / (float)0x3FFF) * renderHeight;
                y2 = (1.0f - curValue / (float)0x3FFF) * renderHeight;

                g.DrawLine(p, x1, y1, x2, y2);

                x1 = x2;
                lastValue = curValue;
            }
        }

    }
}
