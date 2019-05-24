using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using System;
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
        private const int LT_CHART_POINTS = 20;
        private const int LT_MIN_TIME_STEP_SECS = 1;

        public SeriesCollection SeriesCollection { get; set; }
        public Func<double, string> DateTimeFormatter { get; set; }

        private ChartValues<Tuple<DateTime, int>> detectedDevicesCountValues;
        private int timeInterval;
        private DispatcherTimer chartRefreshTimer = new DispatcherTimer();


        public UserControlStat() {
            InitializeComponent();

            /* Converts a Tuple<DateTime, int> into chart X, Y values */
            Charting.For<Tuple<DateTime, int>>(Mappers.Xy<Tuple<DateTime, int>>()
                .X(model => model.Item1.Ticks)
                .Y(model => model.Item2));

            DateTimeFormatter = value => new DateTime((long) value).ToString("HH:mm:ss");

            SeriesCollection = new SeriesCollection();

            chartRefreshTimer.Tick += ChartRefreshTimer_Tick;

            /* Stop the timer when the Control is unloaded */
            this.Unloaded += (object sender, RoutedEventArgs e) => {
                if (chartRefreshTimer.IsEnabled) {
                    chartRefreshTimer.Stop();
                }
            };

            DataContext = this;
        }

        private void Real_Time_Button_Click(object sender, RoutedEventArgs e) {
            /* Clear old series and timer (if running) */
            SeriesCollection.Clear();
            if (chartRefreshTimer.IsEnabled) {
                chartRefreshTimer.Stop();
            }

            timeInterval = Convert.ToInt32(timeIntervalPicker.Text);

            yAxis.Title = "Detected Devices Count";

            /* Prepare the starting series, devices count for the preceding 9 time instants is set to 0 */
            detectedDevicesCountValues = new ChartValues<Tuple<DateTime, int>>();

            DateTime time = DateTime.Now;
            time = time.AddSeconds(-9 * App.AppSniffingManager.SniffingPeriod);

            for (int i = 0; i < 9; i++) {
                detectedDevicesCountValues.Add(new Tuple<DateTime, int>(time, 0));
                time = time.AddSeconds(App.AppSniffingManager.SniffingPeriod);
            }

            int val = new Random().Next(30, 50);    // Replace with DB query
            detectedDevicesCountValues.Add(new Tuple<DateTime, int>(time, val));

            SeriesCollection.Add(new LineSeries {
                Title = "Detected Devices",
                Values = detectedDevicesCountValues,
                LineSmoothness = 0
            });

            /* Program the timer */
            chartRefreshTimer.Interval = new TimeSpan(App.AppSniffingManager.SniffingPeriod * 10000000);
            chartRefreshTimer.Start();
        }

        private void ChartRefreshTimer_Tick(object sender, EventArgs e) {
            int val = new Random().Next(30, 50);    // Replace with DB query
            detectedDevicesCountValues.Add(new Tuple<DateTime, int>(DateTime.Now, val));
            detectedDevicesCountValues.RemoveAt(0);
        }

        private void Button_Click(object sender, RoutedEventArgs e) {
            /* Clear old series and timer (if running) */
            SeriesCollection.Clear();
            if (chartRefreshTimer.IsEnabled) {
                chartRefreshTimer.Stop();
            }

            yAxis.Title = "Packets Sent";

            DateTime startInstant = Convert.ToDateTime(dtpStart.Text);
            DateTime stopInstant = Convert.ToDateTime(dtpStop.Text);
            int devNum = Convert.ToInt32(DevNumPickerCol.Text);

            if((stopInstant - startInstant).TotalSeconds < LT_MIN_TIME_STEP_SECS) {
                /* TODO: Notify the user that dates are too close or in the wrong order */
                return;
            }

            /* Define the time step between two consecutive counts */
            long timeStep = (long) (stopInstant - startInstant).TotalSeconds / LT_CHART_POINTS;
            if (timeStep < LT_MIN_TIME_STEP_SECS) {
                timeStep = LT_MIN_TIME_STEP_SECS;
            }

            var detections = App.AppDBManager
                .GetTalkativeDevices(DateToMillis(startInstant), DateToMillis(stopInstant), timeStep*1000, devNum);

            foreach (var pair in detections) {
                var chartValues = new ChartValues<Tuple<DateTime, int>>();

                /* Fill the chart values */

                SeriesCollection.Add(new LineSeries {
                    Title = pair.Key,
                    Values = chartValues
                });
            }
        }

        private long DateToMillis(DateTime date) {
            return date.Ticks / 10000;
        }
    }
}
