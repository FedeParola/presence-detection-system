using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
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

namespace ESP32_Application
{
    /// <summary>
    /// Logica di interazione per UserControlConfig.xaml
    /// </summary>
    public partial class UserControlConfig : UserControl
    {
        ESPdatiGlobali globalData;
        List<ESPmomentanea> ESPcollection;
        public UserControlConfig(ESPdatiGlobali globalData, List<ESPmomentanea> ESPcollection)
        {
            //IPotesi : Indirizzi IP inseriti sempre correttamente e mai duplicati
            InitializeComponent();
            this.globalData = globalData;
            this.ESPcollection = ESPcollection;
            this.GenerateGrid();
            this.WritePrjParameters();
        }

        private void WritePrjParameters()
        {
            lblwidth.Content = "Room width : " + globalData.Width + "m";
            lblhe.Content = "Room height : " + globalData.Height + "m";
            lblch.Content = "ESP channel : " + globalData.Channel;
            lbltimer.Content = "ESP timer : " + globalData.Timer + "s";
        }

        private void GenerateGrid()
        {
            //delete old Rows
            int rows = ESPList.RowDefinitions.Count;
            for (int i = 0; i < rows; i++)
            {
                ESPList.RowDefinitions.RemoveAt(0);
            }
            ESPList.Children.Clear();

            //add new rows
            for (int i = 0; i < globalData.EspNumber; i++)
            {
                RowDefinition rowDef = new RowDefinition();
                rowDef.Height = new GridLength(200);
                ESPList.RowDefinitions.Add(rowDef);
            }

            for (int i = 0; i < globalData.EspNumber; i++)
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
                stplbl.Margin = new Thickness(35, 20, 0, 0);
                Label lblip = new Label();
                lblip.Content = "Indirizzo IP : " + ESPcollection[i].Ipadd;
                lblip.FontSize = 23;
                Label lblstate = new Label();
                lblstate.Content = "Stato ESP : attivo";
                lblstate.FontSize = 23;
                Label lblloc = new Label();
                lblloc.Content = "Posizione (x,y) : " + ESPcollection[i].X + ", " + ESPcollection[i].Y;
                lblloc.FontSize = 23;
                Label lblport = new Label();
                lblport.Content = "Porta : " + ESPcollection[i].Port;
                lblport.FontSize = 23;
                stplbl.Children.Add(lblip);
                stplbl.Children.Add(lblstate);
                stplbl.Children.Add(lblloc);
                stplbl.Children.Add(lblport);
                Grid.SetColumn(stplbl, 0);

                StackPanel stpbtn = new StackPanel();
                Button buttonconf = new Button();
                Button buttonrem = new Button();
                buttonconf.Content = "Configuration";
                buttonrem.Content = "Remove";
                buttonconf.Background = new SolidColorBrush(Color.FromArgb(255, 49, 87, 126));
                buttonrem.Background = new SolidColorBrush(Color.FromArgb(255, 49, 87, 126));
                buttonconf.HorizontalAlignment = HorizontalAlignment.Left;
                buttonrem.HorizontalAlignment = HorizontalAlignment.Left;
                buttonconf.VerticalAlignment = VerticalAlignment.Top;
                buttonrem.VerticalAlignment = VerticalAlignment.Top;
                buttonconf.Width = 180;
                buttonrem.Width = 180;
                buttonconf.Margin = new Thickness(34, 44, -77, 0);
                buttonrem.Margin = new Thickness(34, 44, -77, 0);
                buttonconf.Click += OnGenenicButtonClickConf;
                buttonrem.Click += OnGenenicButtonClickRem;
                buttonconf.Name = ESPcollection[i].Id;
                buttonrem.Name = ESPcollection[i].Id;
                stpbtn.Children.Add(buttonconf);
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

                Grid.SetRow(besterno, i);
                ESPList.Children.Add(besterno);
            }
        }

        private void Button_Click_confG(object sender, RoutedEventArgs e)
        {
            ConfigureParameters parmDialogBox = new ConfigureParameters(globalData);
            parmDialogBox.ShowDialog();
            this.WritePrjParameters();
        }

        private void Button_Click_New(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            ESPconfiguration prjDialogBox = new ESPconfiguration(globalData, ESPcollection, btn.Name );
            prjDialogBox.ShowDialog();
            this.GenerateGrid();
        }

        private void OnGenenicButtonClickConf(object sender, EventArgs e)
        {
            int i;
            string ipadd = null;
            var btn = sender as Button;
            for (i = 0; i < ESPcollection.Count; i++)
            {
                if (ESPcollection[i].Id.Equals(btn.Name))
                {
                    ipadd = ESPcollection[i].Ipadd;
                    break;
                }
            }
            ESPconfiguration prjDialogBox = new ESPconfiguration(globalData, ESPcollection, btn.Name);
            prjDialogBox.ShowDialog();
            this.GenerateGrid();
        }

        private void OnGenenicButtonClickRem(object sender, EventArgs e)
        {
            int i;
            var btn = sender as Button;
            MessageBox.Show("Cliccato btn rem con name = " + btn.Name);
            Configuration config = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
            for (i=0; i<ESPcollection.Count; i++)
            {
                if (ESPcollection[i].Id.Equals(btn.Name))
                {             
                    break;
                }
            }
            config.AppSettings.Settings.Remove(ESPcollection[i].Ipadd);
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
            ESPcollection.RemoveAt(i);
            globalData.EspNumber = globalData.EspNumber - 1;
            this.GenerateGrid();
        }
    }
}
