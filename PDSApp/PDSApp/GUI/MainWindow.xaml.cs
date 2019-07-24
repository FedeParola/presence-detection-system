using System;
using System.Configuration;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Threading;

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
            uscLog = new UserControlLog();
            App.AppSniffingManager.errorHandler = this.SniffingErrorCallback;
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
            controlSniffing.IsEnabled = false;
            if (!App.AppSniffingManager.IsSniffing()) {
                if (App.AppSniffingManager.GetSniffersCount() < 2) {
                    MessageBox.Show("Needed at least 2 sniffers to start sniffing", "Setup Error");
                    controlSniffing.IsEnabled = true;
                    return;
                }

                //start sniffer code here
                statusIcon.Background = Brushes.Orange;
                loadingSpinner.Visibility = Visibility.Visible;
                //disable UserControlConfig tab
                if (usc is UserControlConfig)
                    usc.IsEnabled = false;
                ThreadPool.QueueUserWorkItem(StartSniffing);

            } else {
                //stop sniffer code here
                statusIcon.Background = Brushes.Orange;
                loadingSpinner.Visibility = Visibility.Visible;
                ThreadPool.QueueUserWorkItem(StopSniffing);
            }
        }

        private void StartSniffing(object arg) {
            try {
                App.AppSniffingManager.StartSniffing();
                Dispatcher.Invoke(SniffingStartedCallback);

            } catch (Exception) {
                Dispatcher.Invoke(SniffingStartErrorCallback);
            }
        }

        private void StopSniffing(object arg) {
            App.AppSniffingManager.StopSniffing();
            Dispatcher.Invoke(SniffingStoppedCallback);
        }

        private void SniffingStartedCallback() {
            controlSniffing.IsEnabled = true;
            statusIcon.Background = Brushes.Green;
            controlSniffing.Content = STOP_SNIFFING;
            loadingSpinner.Visibility = Visibility.Hidden;
        }

        private void SniffingStoppedCallback() {
            //enable UserControlConfig tab
            if (usc is UserControlConfig)
                usc.IsEnabled = true;
            controlSniffing.IsEnabled = true;
            statusIcon.Background = Brushes.Red;
            controlSniffing.Content = START_SNIFFING;
            //switch to config control
            GridMain.Children.Clear();
            usc = new UserControlConfig();
            GridMain.Children.Add(usc);
            MainTitle.Text = CONFIG_TAB_TITLE;
            ListViewMenu.SelectedItem=ItemConfig;
            ItemConfig.IsSelected = true;
            loadingSpinner.Visibility = Visibility.Hidden;
        }

        public void SniffingErrorCallback() {
            Dispatcher.Invoke(() => {
                SniffingStoppedCallback();
                MessageBox.Show("There was an error in the sniffing process", "Sniffing Error");
            });
        }

        private void SniffingStartErrorCallback() {
            SniffingStoppedCallback();
            MessageBox.Show("There was an error contacting the sniffers", "Sniffing Start Error");
        }
    }
}
