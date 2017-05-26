using Svg;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace LastPassImporter
{
    public partial class ConverterUi : Form
    {
        public ConverterUi()
        {
            InitializeComponent();
            Load += ConverterUi_Load;
        }

        private async void ConverterUi_Load(object sender, EventArgs e)
        {
            Visible = false;
            var converter = new Converter();
            Visible = true;
            AnimateLoading();

            try
            {
                converter.LoadAndConvert();
            }
            catch (ConverterException ex)
            {
                MessageBox.Show(this, ex.Message, ex.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Unknown exception during conversion!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AnimateLoading()
        {
            var image = Properties.Resources.sprites_transparent;

            Bitmap cropped = null;
            var t = new Timer()
            {
                Enabled = true,
                Interval = 15
            };

            int index = 0;
            t.Tick += (s, e) =>
            {
                cropped?.Dispose();
                Rectangle srcRect = new Rectangle((index++ % 20) * 256, 0, 256, 256);
                cropped = cropped = image.Clone(srcRect, image.PixelFormat);
                pictureBox1.Image = cropped;
            };
            t.Start();
        }
    }
}
