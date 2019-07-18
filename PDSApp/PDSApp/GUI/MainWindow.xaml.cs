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
        const String CONFIG_TAB_TITLE = "ESP Module Configuration";
        const String LOCALIZATION_TAB_TITLE = "Live users Localization";
        const String STATS_TAB_TITLE = "Statistic";
        const String HIDDEN_DEV_TAB_TITLE = "Hidden Devices";
        private UserControlLog uscLog;

        private UserControl usc = null;
        public MainWindow()
        {
            InitializeComponent();
            GridMain.Children.Clear();
            usc = new UserControlConfig();
            GridMain.Children.Add(usc);
            MainTitle.Text = CONFIG_TAB_TITLE;
            ItemLoc.IsEnabled = false;
            ItemStat.IsEnabled = false;
            ItemHidden.IsEnabled = false;
            uscLog = new UserControlLog();
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
                    MainTitle.Text = CONFIG_TAB_TITLE;
                    break;
                case "ItemLoc":
                    usc = new UserControlLoc();
                    GridMain.Children.Add(usc);
                    MainTitle.Text = LOCALIZATION_TAB_TITLE;
                    break;
                case "ItemStat":
                    usc = new UserControlStat();
                    GridMain.Children.Add(usc);
                    MainTitle.Text = STATS_TAB_TITLE;
                    break;
                case "ItemHidden":
                    usc = new UserControlHidden();
                    GridMain.Children.Add(usc);
                    MainTitle.Text = "Hidden Devices Detection";
                    break;
                case "ItemLog":
                    usc = uscLog;
                    GridMain.Children.Add(usc);
                    MainTitle.Text = "Log";
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
            if (!App.AppSniffingManager.IsSniffing()) {
                //start sniffer code here
                statusIcon.Background = Brushes.Orange;
                loadingProgress.Value = 0;
                App.AppSniffingManager.StartSniffing();
                statusIcon.Background = Brushes.Green;
                controlSniffig.Content = STOP_SNIFFING;
                //enable tabs
                ItemLoc.IsEnabled = true;
                ItemStat.IsEnabled = true;
                ItemHidden.IsEnabled = true;
                ItemConfig.IsEnabled = false;
                //disable the currect tab
                usc.IsEnabled = false;
                //swith to another tab
                GridMain.Children.Clear();
                usc = new UserControlLoc();
                GridMain.Children.Add(usc);
                MainTitle.Text = LOCALIZATION_TAB_TITLE;
                //reset progress bar
                loadingProgress.Value = 0;
            }
            else
            {
                //stop sniffer code here
                statusIcon.Background = Brushes.Orange;
                loadingProgress.Value = 0;
                App.AppSniffingManager.StopSniffing();  // If not sniffing returns immediately
                statusIcon.Background = Brushes.Red;
                controlSniffig.Content = START_SNIFFING;
                //enable tabs
                ItemLoc.IsEnabled = false;
                ItemStat.IsEnabled = false;
                ItemHidden.IsEnabled = false;
                ItemConfig.IsEnabled = true;
                //disable the current tab
                usc.IsEnabled = false;
                //switch to config control
                GridMain.Children.Clear();
                usc = new UserControlConfig();
                GridMain.Children.Add(usc);
                MainTitle.Text = CONFIG_TAB_TITLE;
                //reset the progress bar
                loadingProgress.Value = 0;
            }
        }
    }
}
