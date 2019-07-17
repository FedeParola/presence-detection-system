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
        private int iterator;
        private DispatcherTimer movChartRefreshTimer = new DispatcherTimer();
        private String mac;
        private long startTime;
        private long stopTime;
        private long duration;
        //se lo scale factor è minore di 1 non va !
        private double scaleFactor;

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

            movChartRefreshTimer.Tick += MovChartRefreshTimer_Tick;
            //Stop the timer when the Control is unloaded
            this.Unloaded += (object sender, RoutedEventArgs e) => {
                if (movChartRefreshTimer.IsEnabled){
                    movChartRefreshTimer.Stop();
                }
            };

            DataContext = this;
        }

    //LIVE LOCALIZATION
        private void Live_Localization_Button_Click(object sender, RoutedEventArgs e){
            //Stop the timer if already enabled and get the (new) time interval
            if (locChartRefreshTimer.IsEnabled){
                devicesPositions.Clear();
                locChartRefreshTimer.Stop();
            }
            if (movChartRefreshTimer.IsEnabled){
                deviceMovements.Clear();
                movChartRefreshTimer.Stop();
            }
            timeInterval = Convert.ToInt32(timeIntervalPicker.Text) * 60 * 1000;

            //Clears the old series, executes the query against the db and shows the new series on the chart
            LocChart_Update();

            // Program the timer 
            locChartRefreshTimer.Interval = new TimeSpan(App.AppSniffingManager.SniffingPeriod * 10000000);
            locChartRefreshTimer.Start();
        }

        private void LocChartRefreshTimer_Tick(object sender, EventArgs e){
            //Clears the old series, executes the query against the db and shows the new series on the chart
            LocChart_Update();
        }

        private void LocChart_Update(){
            SeriesCollection.Clear();
            devicesPositions.Clear();

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
        private void Start_Animation_Button_Click(object sender, RoutedEventArgs e){
            //Stop the timer if already enabled and get the (new) time interval
            if (locChartRefreshTimer.IsEnabled){
                devicesPositions.Clear();
                locChartRefreshTimer.Stop();
            }
            if (movChartRefreshTimer.IsEnabled){
                deviceMovements.Clear();
                movChartRefreshTimer.Stop();
            }

            //Clears the old series 
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
            duration = Convert.ToInt32(animDuration.Text) * 1000; //ms
            scaleFactor = (stopTime - startTime) / duration; //devo dividere ogni unità di tempo per questo numero 
                                                             //per far sì che l'animazione duri 'duration' 

            //Executes the query against the db and shows the new series on the chart
            //(For now the resolution is fixed (1 sec)... do we want to permit to the final user to decide it?
            //Is 1 sec too little?)
            movements = App.AppDBManager.GetDeviceMovements(mac, startTime, stopTime, 1000);
            if(movements.Count == 0){
                //there is no record in the db that satisfies the requirements 
                return;
            }
            iterator = 0;

            //Add the new position on the chart and set the new refresh timer (or stop it if it was the last position)
            MovChart_Update();
        }

        private void MovChartRefreshTimer_Tick(object sender, EventArgs e){
            //Add the new position on the chart and set the new refresh timer (or stop it if it was the last position)
            MovChart_Update();
        }

        //Il timeout cambia di volta in volta, ed è pari alla differenza di secondi tra la prossima posizione da mostrare
        // e quella appena mostrata, diviso lo scale factor
        private void MovChart_Update(){
            Location loc = movements[iterator];
            deviceMovements.Add(new ChartValues<ScatterPoint>{
                new ScatterPoint(loc.Position.X, loc.Position.Y)
            });
            SeriesCollection.Add(new ScatterSeries{
                Title = "Timestamp: " + DateTimeOffset.FromUnixTimeMilliseconds(loc.Timestamp).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"), //timestamp  
                Values = deviceMovements[iterator],  //position of the device
                MinPointShapeDiameter = 15,
                MaxPointShapeDiameter = 15
            });

            if (++iterator < movements.Count){
                long timer = (long)((movements[iterator].Timestamp - movements[iterator - 1].Timestamp) / scaleFactor);
                // Program the timer 
                movChartRefreshTimer.Interval = new TimeSpan(timer * 10000);
                movChartRefreshTimer.Start();
            }
            else if (movChartRefreshTimer.IsEnabled){
                // We plotted all the positions: stop the refresh timer
                movChartRefreshTimer.Stop();
            }
        }
    }
}
