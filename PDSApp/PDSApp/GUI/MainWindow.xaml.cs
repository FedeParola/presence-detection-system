using System;
using System.Configuration;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace PDSApp.GUI {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow : Window
    {
        const String START_SNIFFING = "Start Sniffing";
        const String STOP_SNIFFING = "Stop Sniffing";
        public MainWindow()
        {
            InitializeComponent();
            GridMain.Children.Clear();
            UserControl usc = new UserControlLoc();
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
                    usc = new UserControlConfig();                
                    GridMain.Children.Add(usc);
                    MainTitle.Text = "ESP Module Configuration";
                    break;
                case "ItemLoc":
                    usc = new UserControlLoc();
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

        private void StartSniffing_Click(object sender, RoutedEventArgs e)
        {
            //if (App.AppSniffingManager.IsSniffing())
            if(controlSniffig.Content.Equals(START_SNIFFING))
            {
                //stop sniffer code here
                App.AppDBManager.CloseConn();
                App.AppSniffingManager.StopSniffing();  // If not sniffing returns immediately
                controlSniffig.Content = STOP_SNIFFING;
                statusIcon.Background = Brushes.Green;
            }
            else
            {
                //start sniffer code here
                //App.AppSniffingManager.StartSniffing();
                controlSniffig.Content = START_SNIFFING;
                statusIcon.Background = Brushes.Red;
            }
        }
    }
}
