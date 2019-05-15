using System;
using System.Configuration;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Cinchoo.Core;
using Cinchoo.Core.Configuration;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ESP32_Application
{
    /// <summary>
    /// Interação lógica para MainWindow.xam
    /// </summary>

    public partial class MainWindow : Window
    {
        ESPdatiGlobali globalData;
        List<ESPmomentanea> ESPcollection;
        public MainWindow()
        {

            //Qua leggo il file di configurazione e salvo i dati nelle classi già create. 
            //Momentaneamente creo una classi di appoggio che contengono tutti i dati che mi servono . 
            ESPcollection = new List<ESPmomentanea>();
            NameValueCollection appSettings = ConfigurationManager.AppSettings;

            globalData = new ESPdatiGlobali((appSettings.Count-4), 
                Int32.Parse(ConfigurationManager.AppSettings["channel"]),
                Int32.Parse(ConfigurationManager.AppSettings["width"]), 
                Int32.Parse(ConfigurationManager.AppSettings["height"]),
                Int32.Parse(ConfigurationManager.AppSettings["timer"]));

            for (int i=4; i<appSettings.Count; i++)
            {
                string[] value = appSettings[i].Split(",");
                string[] position = value[1].Split(";");
                int x = Int32.Parse(position[0]);
                int y = Int32.Parse(position[1]);
                ESPmomentanea esp = new ESPmomentanea(GenerateID(appSettings.GetKey(i)), appSettings.GetKey(i), value[0], x, y);
                ESPcollection.Add(esp);
            }
            InitializeComponent();

        }

        private void ButtonOpenMenu_Click(object sender, RoutedEventArgs e)
        {
            ButtonCloseMenu.Visibility = Visibility.Visible;
            ButtonOpenMenu.Visibility = Visibility.Collapsed;
        }

        private void ButtonCloseMenu_Click(object sender, RoutedEventArgs e)
        {
            ButtonCloseMenu.Visibility = Visibility.Collapsed;
            ButtonOpenMenu.Visibility = Visibility.Visible;
        }

        private void ListViewMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UserControl usc = null;
            GridMain.Children.Clear();

            switch (((ListViewItem)((ListView)sender).SelectedItem).Name)
            {
                case "ItemConfig":
                    usc = new UserControlConfig(globalData, ESPcollection);                
                    GridMain.Children.Add(usc);
                    MainTitle.Text = "ESP Module Configuration";
                    break;
                case "ItemLoc":
                    usc = new UserControlLoc(globalData);
                    GridMain.Children.Add(usc);
                    MainTitle.Text = "Live users Localization";
                    break;
                case "ItemStat":
                    usc = new UserControlStat();
                    GridMain.Children.Add(usc);
                    MainTitle.Text = "Statistic";
                    break;
                case "ItemMov":
                    usc = new UserControlMov();
                    GridMain.Children.Add(usc);
                    MainTitle.Text = "Localization History";
                    break;
                default:
                    break;
            }
        }

        private void ButtonExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        public static string GenerateID(string ipa)
        {
            string id;
            string[] value = ipa.Split(".");
            id = "id" + value[0] + value[1] + value[2] + value[3];
            return id;
        }

        public static Boolean CheckIP(string ip)
        {
            Match match = Regex.Match(ip, @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}");
            if (match.Success)
            {
                return true;
            }
            return false;
        }
    }
}
