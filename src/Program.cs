using System;
using System.Windows.Forms;
using ricxOptimizer.UI;

namespace ricxOptimizer
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += (s, e) =>
            {
                MessageBox.Show(
                    $"Ocorreu um erro no ricxOptimizer:\n\n{e.Exception.Message}",
                    "ricxOptimizer - Erro",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    MessageBox.Show(
                        $"Erro crítico não tratado:\n\n{ex.Message}",
                        "ricxOptimizer - Erro Crítico",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            };

            Application.Run(new MainForm());
        }
    }
}
