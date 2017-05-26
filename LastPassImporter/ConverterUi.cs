using Svg;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LastPassImporter
{
    public partial class ConverterUi : Form
    {
        private Converter _converter;
        private System.Threading.Timer _animator = null;

        public ConverterUi(Converter converter)
        {
            _converter = converter;
            converter.ConversionCompleted += (b) =>
            {
                _animator.Dispose();
                Invoke((Action)(() => Close()));
            };

            InitializeComponent();
            Load += ConverterUi_Load;
        }

        private void ConverterUi_Load(object sender, EventArgs e)
        {
            SetForegroundWindow(Handle.ToInt32());
            AnimateLoading();
        }

        private void AnimateLoading()
        {
            var image = Properties.Resources.sprites_transparent;

            int index = 0;
            Bitmap cropped = null;
            _animator = new System.Threading.Timer((o) =>
            {
                Invoke((Action)(() =>
                {
                    cropped?.Dispose();
                    Rectangle srcRect = new Rectangle((index++ % 20) * 256, 0, 256, 256);
                    cropped = cropped = image.Clone(srcRect, image.PixelFormat);
                    pictureBox1.Image = cropped;
                }));
            }, null, 0, 15);
        }

        [DllImport("User32.dll")]
        public static extern Int32 SetForegroundWindow(int hWnd);
    }
}
