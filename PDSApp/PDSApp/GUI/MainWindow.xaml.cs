using System;
using System.Configuration;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions;

namespace PDSApp.GUI {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
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

            globalData = new ESPdatiGlobali((appSettings.Count-5), 
                Int32.Parse(ConfigurationManager.AppSettings["channel"]),
                Int32.Parse(ConfigurationManager.AppSettings["width"]), 
                Int32.Parse(ConfigurationManager.AppSettings["length"]),
                Int32.Parse(ConfigurationManager.AppSettings["timer"]),
                Int32.Parse(ConfigurationManager.AppSettings["port"]));

            for (int i=5; i<appSettings.Count; i++)
            {
                string[] position = appSettings[i].Split(";");
                int x = Int32.Parse(position[0]);
                int y = Int32.Parse(position[1]);
                ESPmomentanea esp = new ESPmomentanea(GenerateID(appSettings.GetKey(i)), appSettings.GetKey(i), "attivo", x, y);
                ESPcollection.Add(esp);
            }
            InitializeComponent();
            GridMain.Children.Clear();
            UserControl usc = new UserControlLoc(globalData);
            GridMain.Children.Add(usc);
            MainTitle.Text = "Live users Localization";
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
