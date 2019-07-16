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

        private List<ChartValues<ScatterPoint>> devicesPositions;
        private DispatcherTimer chartRefreshTimer = new DispatcherTimer();
        private long timeInterval;
        private NumberFormatInfo nfi;
        public UserControlLoc()
        {
            InitializeComponent();

            xAxis = App.AppSniffingManager.RoomLength;
            yAxis = App.AppSniffingManager.RoomWidth;
            nfi = new NumberFormatInfo();
            nfi.NumberDecimalSeparator = ".";
            XFormatter = value => value.ToString("0.##", nfi);
            YFormatter = value => value.ToString("0.##", nfi);

            SeriesCollection = new SeriesCollection();
            devicesPositions = new List<ChartValues<ScatterPoint>>();

            chartRefreshTimer.Tick += ChartRefreshTimer_Tick;

            /* Stop the timer when the Control is unloaded */
            this.Unloaded += (object sender, RoutedEventArgs e) => {
                if (chartRefreshTimer.IsEnabled)
                {
                    chartRefreshTimer.Stop();
                }
            };

            DataContext = this;
        }

        private void Real_Time_Button_Click(object sender, RoutedEventArgs e)
        {
            /*Stop the timer if already enabled and get the (new) time interval*/
            if (chartRefreshTimer.IsEnabled)
            {
                chartRefreshTimer.Stop();
            }
            timeInterval = Convert.ToInt32(timeIntervalPicker.Text) * 60 * 1000;

            /*Clears the old series, executes the query against the db and shows the new series on the chart*/
            Chart_Update();

            /* Program the timer */
            chartRefreshTimer.Interval = new TimeSpan(App.AppSniffingManager.SniffingPeriod * 10000000);
            chartRefreshTimer.Start();
        }

        private void ChartRefreshTimer_Tick(object sender, EventArgs e)
        {
            /*Clears the old series, executes the query against the db and shows the new series on the chart*/
            Chart_Update();
        }

        private void Chart_Update()
        {
            SeriesCollection.Clear();
            devicesPositions.Clear();

            List<Tuple<String, Location>> positions =
                /*For all the devices detected since (now-timeInterval) compute and return the latest position*/
                App.AppDBManager.EstimateDevicesPosition(timeInterval);
            for (int i = 0; i < positions.Count; i++)
            {
                String mac = positions[i].Item1;
                Location loc = positions[i].Item2;
                devicesPositions.Add(new ChartValues<ScatterPoint>
                {
                    new ScatterPoint(loc.Position.X, loc.Position.Y)
                });

                SeriesCollection.Add(new ScatterSeries
                {
                    Title = mac +                                                    //MAC of the detected device
                    "\r\nTime: " + new DateTime(loc.Timestamp).ToString("HH:mm:ss"), //timestamp  
                    Values = devicesPositions[i],                                    //position of the device
                    MinPointShapeDiameter = 15,
                    MaxPointShapeDiameter = 15
                });
            }
        }
    }
}
