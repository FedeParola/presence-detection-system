using System;
using System.Windows;
using System.Windows.Controls;

namespace PDSApp.GUI {
    /// <summary>
    /// Logica di interazione per UserControlLoc.xaml
    /// </summary>
    public partial class UserControlLoc : UserControl
    {
        public UserControlLoc()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn.Name.Equals("btnStart"))
            {
                UserControlConfig.setMyStatus(true);
            }
            else
            {
                UserControlConfig.setMyStatus(false);
            }
            
        }
    }
}
