using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace PDSApp.GUI {
    /// <summary>
    /// Interaction logic for UserControlLog.xaml
    /// </summary>
    public partial class UserControlLog : UserControl {

        public UserControlLog() {
            InitializeComponent();
            //Console.SetOut(new LogWriter(txtLog));
        }

        private void Clear_Click(object sender, RoutedEventArgs e) {
            txtLog.Text = "";
            return;
        }

        public class LogWriter : TextWriter {
            private TextBox textbox;
            public LogWriter(TextBox textbox) {
                this.textbox = textbox;
            }

            public override void Write(char value) {
                textbox.AppendText(value.ToString());
            }

            public override void Write(string value) {
                textbox.AppendText(value);
                textbox.ScrollToEnd();
            }

            public override void WriteLine(string value) {
                textbox.Text += value+"\n";
                textbox.ScrollToEnd();
            }

            public override Encoding Encoding {
                get { return Encoding.ASCII; }
            }
        }
    }
}
