using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PDSApp.GUI {
    /// <summary>
    /// Logica di interazione per UserControlStat.xaml
    /// </summary>
    public partial class UserControlStat : UserControl {
        private const int CHART_POINTS_COUNT = 10;

        public SeriesCollection SeriesCollection { get; set; }
        public Func<double, string> DateTimeFormatter { get; set; }

        private ChartValues<Tuple<DateTime, int>> detectedDevicesCountValues;
        private int timeInterval;
        private DispatcherTimer chartRefreshTimer = new DispatcherTimer();


        public UserControlStat()
        {
            InitializeComponent();

            Charting.For<Tuple<DateTime, int>>(Mappers.Xy<Tuple<DateTime, int>>()
                .X(model => model.Item1.Ticks)
                .Y(model => model.Item2));

            DateTimeFormatter = value => new DateTime((long) value).ToString("HH:mm:ss");
        }

        private void Real_Time_Button_Click(object sender, RoutedEventArgs e)
        {
            timeInterval = Convert.ToInt32(timeIntervalPicker.Text);

            yAxis.Title = "Detected Devices Count";

            detectedDevicesCountValues = new ChartValues<Tuple<DateTime, int>>();

            DateTime time = DateTime.Now;
            time = time.AddSeconds(-9 * App.AppSniffingManager.SniffingPeriod);

            for (int i = 0; i < 9; i++) {
                detectedDevicesCountValues.Add(new Tuple<DateTime, int>(time, 0));
                time = time.AddSeconds(App.AppSniffingManager.SniffingPeriod);
            }

            int val = new Random().Next(30, 50);
            detectedDevicesCountValues.Add(new Tuple<DateTime, int>(time, val));

            SeriesCollection = new SeriesCollection {
                new LineSeries {
                    Title = "Detected Devices",
                    Values = detectedDevicesCountValues
                }
            };

            chartRefreshTimer.Tick += ChartRefreshTimer_Tick;
            chartRefreshTimer.Interval = new TimeSpan(App.AppSniffingManager.SniffingPeriod * 10000000);
            chartRefreshTimer.Start();

            DataContext = this;
        }

        private void ChartRefreshTimer_Tick(object sender, EventArgs e) {
            int val = new Random().Next(30, 50);

            detectedDevicesCountValues.Add(new Tuple<DateTime, int>(DateTime.Now, val));
            detectedDevicesCountValues.RemoveAt(0);
        }

        private void Button_Click(object sender, RoutedEventArgs e) {
        }


    }
}
