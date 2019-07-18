using PDSApp.Persistence;
using PDSApp.SniffingManagement;
using System;
using System.Configuration;
using System.Collections.Specialized;
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
            NameValueCollection appSettings = ConfigurationManager.AppSettings;
            AppDBManager = new DBManager("127.0.0.1", "user", "pass", "pds");
            AppSniffingManager = new SniffingManager(UInt16.Parse(ConfigurationManager.AppSettings["port"]),
                                                     UInt16.Parse(ConfigurationManager.AppSettings["timer"]),
                                                     Byte.Parse(ConfigurationManager.AppSettings["channel"]),
                                                     Double.Parse(ConfigurationManager.AppSettings["length"]),
                                                     Double.Parse(ConfigurationManager.AppSettings["width"]),
                                                     AppDBManager, () => Console.WriteLine("Error Callback"));
            for (int i = 5; i < appSettings.Count; i++)
            {
                string[] position = appSettings[i].Split(";");
                int x = Int32.Parse(position[0]);
                int y = Int32.Parse(position[1]);
                AppSniffingManager.AddSniffer(new Sniffer(appSettings.GetKey(i), new PDSApp.SniffingManagement.Trilateration.Point(x, y)));
            }
            
        }

        private void App_Exit(object sender, ExitEventArgs e) {
            AppDBManager.CloseConn();
            AppSniffingManager.StopSniffing();  // If not sniffing returns immediately
        }
    }
}
