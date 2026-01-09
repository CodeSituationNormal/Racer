using RacerWF;
using System;
using System.Windows.Forms;

namespace RacerWF  // замените на им€ вашего пространства имЄн, если другое
{
    internal static class Program
    {
        /// <summary>
        ///  √лавна€ точка входа дл€ приложени€.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());  // Form1 Ч это им€ вашей главной формы
        }
    }
}