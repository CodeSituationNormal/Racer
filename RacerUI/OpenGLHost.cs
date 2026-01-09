using System.Windows.Forms;

namespace RacerWF
{
    public class OpenGLHost : Panel
    {
        public OpenGLHost()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.Opaque,
                true);

            DoubleBuffered = false;
            UpdateStyles();
        }

        protected override void OnPaint(PaintEventArgs e)
        {

        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {

        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;

                cp.Style |= 0x02000000;
                cp.Style |= 0x04000000;

                return cp;
            }
        }
    }
}
