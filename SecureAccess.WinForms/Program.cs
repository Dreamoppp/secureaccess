using SecureAccess.WinForms.Forms;

namespace SecureAccess.WinForms;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // Set high DPI settings and default enterprise application configuration
        ApplicationConfiguration.Initialize();
        Application.Run(new LoginForm());
    }    
}