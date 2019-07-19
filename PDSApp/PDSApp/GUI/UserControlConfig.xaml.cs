using PDSApp.SniffingManagement;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PDSApp.GUI {
    /// <summary>
    /// Logica di interazione per UserControlConfig.xaml
    /// </summary>
    public partial class UserControlConfig : UserControl
    {
        static Boolean statusS = false; //false = esp system not working
        public UserControlConfig()
        {
            //IPotesi : Indirizzi IP inseriti sempre correttamente e mai duplicati
            InitializeComponent();
            this.GenerateGrid();
            this.WritePrjParameters();
            //disable UserControlConfig tab
            if (App.AppSniffingManager.IsSniffing() || App.AppSniffingManager.IsStarting())
                this.IsEnabled = false;
        }

        private void WritePrjParameters()
        {
            //enable/disable buttons checking status
            if (statusS)
                confPar.IsEnabled = false;
            if (statusS)
                buttonNew.IsEnabled = false;

            //write prjParameters
            lblhe.Content = "Room length (x) : " + App.AppSniffingManager.RoomLength + "m";
            lblwidth.Content = "Room width (y) : " + App.AppSniffingManager.RoomWidth + "m";
            lblch.Content = "WiFi channel : " + App.AppSniffingManager.Channel;
            lbltimer.Content = "Sniffing period : " + App.AppSniffingManager.SniffingPeriod + "s";
            lblPort.Content = "ESP port : " + App.AppSniffingManager.Port;
        }

        private void GenerateGrid()
        {
            int rowN = 0;
            //delete old Rows
            int rows = ESPList.RowDefinitions.Count;
            for (int i = 0; i < rows; i++)
            {
                ESPList.RowDefinitions.RemoveAt(0);
            }
            ESPList.Children.Clear();

            //add new rows
            for (int i = 0; i < App.AppSniffingManager.GetSniffersCount(); i++)
            {
                RowDefinition rowDef = new RowDefinition();
                rowDef.Height = new GridLength(200);
                ESPList.RowDefinitions.Add(rowDef);
            }

            foreach (Sniffer s in App.AppSniffingManager.GetSniffers())
            {
                Border besterno = new Border();
                besterno.BorderBrush = Brushes.Black;
                besterno.BorderThickness = new Thickness(0, 0, 0, 3);
                besterno.Margin = new Thickness(20, 0, 20, 0);

                Grid myRowGrid = new Grid();
                ColumnDefinition gridCol1 = new ColumnDefinition();
                ColumnDefinition gridCol2 = new ColumnDefinition();
                gridCol1.Width = new GridLength(550);
                gridCol2.Width = GridLength.Auto;
                myRowGrid.ColumnDefinitions.Add(gridCol1);
                myRowGrid.ColumnDefinitions.Add(gridCol2);


                StackPanel stplbl = new StackPanel();
                stplbl.Margin = new Thickness(35, 30, 0, 0);
                Label lblip = new Label();
                lblip.Content = "IP Address: " + s.Ip;
                lblip.FontSize = 23;
                Label lblloc = new Label();
                lblloc.Content = "Position (x; y) : " + s.Position.X + "; " + s.Position.Y;
                lblloc.FontSize = 23;
                stplbl.Children.Add(lblip);
                stplbl.Children.Add(lblloc);
                Grid.SetColumn(stplbl, 0);

                StackPanel stpbtn = new StackPanel();
                Button buttonrem = new Button();
                if (statusS)
                    buttonrem.IsEnabled = false;
                buttonrem.Content = "Remove";
                buttonrem.Background = new SolidColorBrush(Color.FromArgb(255, 49, 87, 126));
                buttonrem.HorizontalAlignment = HorizontalAlignment.Left;
                buttonrem.VerticalAlignment = VerticalAlignment.Top;
                buttonrem.Width = 180;
                buttonrem.Margin = new Thickness(34, 44, -77, 0);
                buttonrem.Click += OnGenenicButtonClickRem;
                buttonrem.Name = GenerateID(s.Ip);
                stpbtn.Children.Add(buttonrem);

                Border binterno = new Border();
                binterno.BorderBrush = Brushes.Black;
                binterno.BorderThickness = new Thickness(3, 0, 0, 0);
                binterno.Margin = new Thickness(0, 10, 10, 10);
                binterno.Child = stpbtn;
                Grid.SetColumn(binterno, 1);

                myRowGrid.Children.Add(stplbl);
                myRowGrid.Children.Add(binterno);

                besterno.Child = myRowGrid;

                Grid.SetRow(besterno, rowN);
                ESPList.Children.Add(besterno);
                rowN++;
            }
        }

        private void Button_Click_confG(object sender, RoutedEventArgs e)
        {
            ConfigureParameters parmDialogBox = new ConfigureParameters();
            parmDialogBox.ShowDialog();
            this.WritePrjParameters();
        }

        private void Button_Click_New(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            ESPconfiguration prjDialogBox = new ESPconfiguration(btn.Name);
            prjDialogBox.ShowDialog();
            this.GenerateGrid();
        }

        private void OnGenenicButtonClickConf(object sender, EventArgs e)
        {
            string ipadd = null;
            var btn = sender as Button;
            foreach (Sniffer s in App.AppSniffingManager.GetSniffers())
            {
                if (GenerateID(s.Ip).Equals(btn.Name))
                {
                    ipadd = s.Ip;
                    break;
                }
            }
            ESPconfiguration prjDialogBox = new ESPconfiguration(btn.Name);
            prjDialogBox.ShowDialog();
            this.GenerateGrid();
        }

        private void OnGenenicButtonClickRem(object sender, EventArgs e)
        {
            var btn = sender as Button;
            System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
            foreach (Sniffer s in App.AppSniffingManager.GetSniffers())
            {
                if (GenerateID(s.Ip).Equals(btn.Name))
                {
                    App.AppSniffingManager.RemoveSniffer(s.Ip);
                    config.AppSettings.Settings.Remove(s.Ip);
                    config.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");
                    this.GenerateGrid();
                    break;
                }
            }
            
        }

        public static void setMyStatus(Boolean v)
        {
            statusS = v;
        }

        public static string GenerateID(string ipa)
        {
            string id;
            string[] value = ipa.Split(".");
            id = "id" + value[0] + value[1] + value[2] + value[3];
            return id;
        }
    }
}
