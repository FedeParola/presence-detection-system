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
            AppSniffingManager = new SniffingManager(13000, 30, 1, 5, 5, AppDBManager, () => Console.WriteLine("Error") );
            AppSniffingManager.AddSniffer(new Sniffer("192.168.1.84", new PDSApp.SniffingManagement.Trilateration.Point(0, 0)));
            AppSniffingManager.AddSniffer(new Sniffer("192.168.1.85", new PDSApp.SniffingManagement.Trilateration.Point(5, 5)));
            //AppSniffingManager.StartSniffing();
        }

        private void App_Exit(object sender, ExitEventArgs e) {
            AppDBManager.CloseConn();
            AppSniffingManager.StopSniffing();  // If not sniffing returns immediately
        }
    }
}
