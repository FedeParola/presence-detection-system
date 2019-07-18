using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using PDSApp.Persistence;
using System.Text.RegularExpressions;

namespace PDSApp.GUI {
    /// <summary>
    /// Logica di interazione per UserControlLoc.xaml
    /// </summary>
    public partial class UserControlLoc : UserControl
    {
        public double xAxis { get; }
        public double yAxis { get; }
        public Func<double, string> XFormatter { get; set; }
        public Func<double, string> YFormatter { get; set; }
        public SeriesCollection SeriesCollection { get; set; }

        private NumberFormatInfo nfi;

        //LIVE LOCALIZATION
        private List<ChartValues<ScatterPoint>> devicesPositions;
        private DispatcherTimer locChartRefreshTimer = new DispatcherTimer();
        private long timeInterval;

        //DEVICE ANIMATION
        private List<ChartValues<ScatterPoint>> deviceMovements;
        private List<Location> movements;
        private String mac;
        private long startTime;
        private long stopTime;

        public UserControlLoc(){
            InitializeComponent();

            xAxis = App.AppSniffingManager.RoomLength;
            yAxis = App.AppSniffingManager.RoomWidth;
            nfi = new NumberFormatInfo();
            nfi.NumberDecimalSeparator = ".";
            XFormatter = value => value.ToString("0.##", nfi);
            YFormatter = value => value.ToString("0.##", nfi);

            SeriesCollection = new SeriesCollection();

            //LIVE LOCALIZATION
            devicesPositions = new List<ChartValues<ScatterPoint>>();

            locChartRefreshTimer.Tick += LocChartRefreshTimer_Tick;
            //Stop the timer when the Control is unloaded
            this.Unloaded += (object sender, RoutedEventArgs e) => {
                if (locChartRefreshTimer.IsEnabled){
                    locChartRefreshTimer.Stop();
                }
            };

            //DEVICE ANIMATION
            deviceMovements = new List<ChartValues<ScatterPoint>>();

            DataContext = this;
        }

    //LIVE LOCALIZATION
        private void Live_Localization_Button_Click(object sender, RoutedEventArgs e){
            //Stop the timer if already enabled and get the (new) time interval
            if (locChartRefreshTimer.IsEnabled){
                locChartRefreshTimer.Stop();
            }
            timeInterval = Convert.ToInt32(timeIntervalPicker.Text) * 60 * 1000;

            deviceMovements.Clear(); //serve? potrebbe esserci la serie relativa all'altra tab ma forse fare la clear della series 
            //collection (fatta in locChartUpdate) basta...
            //Clear the old series, executes the query against the db and shows the new series on the chart
            LocChart_Update();

            // Program the timer 
            locChartRefreshTimer.Interval = new TimeSpan(App.AppSniffingManager.SniffingPeriod * 10000000);
            locChartRefreshTimer.Start();
        }

        private void LocChartRefreshTimer_Tick(object sender, EventArgs e){
            //Clear the old series, execute the query against the db and show the new series on the chart
            LocChart_Update();
        }

        private void LocChart_Update(){
            devicesPositions.Clear();
            SeriesCollection.Clear();

            //For all the devices detected since (now-timeInterval) compute and return the latest position
            List<Tuple<String, Location>> positions = App.AppDBManager.EstimateDevicesPosition(timeInterval);
            for (int i = 0; i < positions.Count; i++){
                String mac = positions[i].Item1;
                Location loc = positions[i].Item2;
                devicesPositions.Add(new ChartValues<ScatterPoint>{
                    new ScatterPoint(loc.Position.X, loc.Position.Y)
                });

                SeriesCollection.Add(new ScatterSeries{
                    Title = mac +  //MAC of the detected device
                    "\r\nTime: " + DateTimeOffset.FromUnixTimeMilliseconds(loc.Timestamp).LocalDateTime.ToString("HH:mm:ss"), //timestamp 
                    Values = devicesPositions[i],  //position of the device
                    MinPointShapeDiameter = 15,
                    MaxPointShapeDiameter = 15
                });
            }
        }

    //DEVICE ANIMATION
        private void Compute_Movements_Button_Click(object sender, RoutedEventArgs e){
            //Stop the locChartTimer if enabled
            if (locChartRefreshTimer.IsEnabled){
                devicesPositions.Clear();
                locChartRefreshTimer.Stop();
            }

            //Clear the old series 
            deviceMovements.Clear();
            SeriesCollection.Clear();

            //Get the (possibly new) value of the parameters
            mac = macAddr.Text;
            mac = mac.ToUpper();
            if (animStart.Value.IsNull() || animStop.Value.IsNull()){
                MessageBox.Show("Set the time interval", "Invalid input");
                return;
            }
            DateTime startDate = DateTime.ParseExact(animStart.Text, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            DateTime stopDate = DateTime.ParseExact(animStop.Text, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            startTime = new DateTimeOffset(startDate).ToUnixTimeMilliseconds();
            stopTime = new DateTimeOffset(stopDate).ToUnixTimeMilliseconds();
            if (stopTime < startTime){
                MessageBox.Show("Invalid time interval", "Invalid input");
                return;
            }

            //Executes the query against the db and shows the new series on the chart
            //(For now the resolution is fixed (1 sec)... do we want to permit to the final user to decide it?
            //Is 1 sec too little?)
            movements = App.AppDBManager.GetDeviceMovements(mac, startTime, stopTime, 1000);
            if (movements.Count == 0){
                //no packets were found in the given time interval 
                MessageBox.Show("The device was not detected in the given time interval!", "Warning");
                return; 
            }
            for(int i = 0; i < movements.Count; i++){
                deviceMovements.Add(new ChartValues<ScatterPoint>{
                    new ScatterPoint(movements[i].Position.X, movements[i].Position.Y)
                });
            }
            slider.Maximum = movements.Count - 1;
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e){
            int i = Convert.ToInt32(e.NewValue);
            Location loc = movements[i];
            
            //Update the slider label
            String timestamp = DateTimeOffset.FromUnixTimeMilliseconds(movements[i].Timestamp).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            string msg = String.Format("Timestamp: " + timestamp);
            positionTimestamp.Text = msg;

            //Update the chart
            SeriesCollection.Clear();
            SeriesCollection.Add(new ScatterSeries{
                Title = "Timestamp: " + DateTimeOffset.FromUnixTimeMilliseconds(loc.Timestamp).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"), //timestamp  
                Values = deviceMovements[i],  //position of the device
                MinPointShapeDiameter = 15,
                MaxPointShapeDiameter = 15
            });
        }
    }
}
