using PDSApp.Persistence;
using PDSApp.SniffingManagement;
using System;
using System.Windows;

namespace PDSApp {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        /* Application global objects */
        internal static DBManager AppDBManager { get; private set; }
        internal static SniffingManager AppSniffingManager { get; private set; }

        private void App_Startup(object sender, StartupEventArgs e) {
            AppDBManager = new DBManager("127.0.0.1", "user", "pass", "pds");
            AppSniffingManager = new SniffingManager(13000, 10, 13, 5, 5, AppDBManager, () => Console.WriteLine("Error") );
        }

        private void App_Exit(object sender, ExitEventArgs e) {
            AppDBManager.CloseConn();
            AppSniffingManager.StopSniffing();  // If not sniffing returns immediately
        }
    }
}
