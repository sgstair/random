using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace infinite_dots
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            DoubleBuffered = true;
            Paint += Form1_Paint;

            Timer t = new Timer();
            t.Tick += t_Tick;
            t.Interval = 40;
            t.Start();

            start = DateTime.Now;
        }

        DateTime start;

        void t_Tick(object sender, EventArgs e)
        {
            Invalidate();
        }

        void Form1_Paint(object sender, PaintEventArgs e)
        {
            double time = DateTime.Now.Subtract(start).TotalSeconds;

            render(e.Graphics, -time / 2);
        }



        double width, height;
        double centerx, centery;

        void render(Graphics g, double t)
        {
            t = t - Math.Floor(t);

            width = g.ClipBounds.Width;
            height = g.ClipBounds.Height;
            centerx = width / 2;
            centery = height / 2;


            g.Clear(Color.LightBlue);

            int stages = 30;
            int basedist = 1;
            for (int i = 0; i <= stages; i++)
            {
                double z = basedist + stages - i + t;

                if (z == 0) continue;

                double span = basedist / z;

                double size = 25 * span;
                double stepdistance = span * width;

                renderstage(g, size, stepdistance);

            }

        }

        void renderstage(Graphics g, double size, double step)
        {
            int maxx = (int)Math.Floor((width / 2 + step / 2 + size) / step);
            int maxy = (int)Math.Floor((height / 2 + step / 2 + size) / step);

            for (int y = -maxy; y < maxy; y++)
            {
                for (int x = -maxx; x < maxx; x++)
                {
                    double cx = centerx + step / 2 + step * x;
                    double cy = centery + step / 2 + step * y;

                    g.FillEllipse(Brushes.Black, (float)(cx - size / 2), (float)(cy - size / 2), (float)size, (float)size);

                }
            }
        }

    }
}
