using System;
using System.Configuration;
using System.Windows.Forms;
using MaterialSkin;

namespace PDSApp.GUI {
    public partial class ConfigureParameters : MaterialSkin.Controls.MaterialForm
    {
        MaterialSkin.MaterialSkinManager skinManager;
        public ConfigureParameters()
        {
            InitializeComponent();
            skinManager = MaterialSkinManager.Instance;
            skinManager.AddFormToManage(this);
            skinManager.Theme = MaterialSkinManager.Themes.LIGHT;
            skinManager.ColorScheme = new ColorScheme(Primary.BlueGrey700, Primary.Grey600, Primary.Grey900, Accent.LightBlue200, TextShade.WHITE);
            showCurrentParamsInWindow(ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath));
        }

        private void showCurrentParamsInWindow(Configuration config)
        {
            if (config != null)
            {
                txtH.Text = config.AppSettings.Settings["length"].Value;
                txtW.Text = config.AppSettings.Settings["width"].Value;
                txtCh.Text = config.AppSettings.Settings["channel"].Value;
                txtTim.Text = config.AppSettings.Settings["timer"].Value;
                txtPort.Text = config.AppSettings.Settings["port"].Value;
                txtH.Select();
            }
        }

        private void buttonSavePrj_Click(object sender, EventArgs e)
        {
            String textH = txtH.Text;
            String textW = txtW.Text;
            String textCh = txtCh.Text;
            String textTim = txtTim.Text;
            String textPor = txtPort.Text;
            Boolean error = false;
            Configuration config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath);

            
            if (!string.IsNullOrWhiteSpace(textH))
            {
                try
                {
                    App.AppSniffingManager.RoomLength = Double.Parse(textH);
                    config.AppSettings.Settings["length"].Value = textH;
                    config.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");
                }
                catch (Exception)
                {
                    error = true;
                    MessageBox.Show("Enter a valid length parameter", "Invalid parameter");
                    txtH.Text = config.AppSettings.Settings["length"].Value;
                }

            }
            if (!string.IsNullOrWhiteSpace(textW))
            {
                try
                {
                    App.AppSniffingManager.RoomWidth = Double.Parse(textW);
                    config.AppSettings.Settings["width"].Value = textW;
                    config.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");
                }
                catch (Exception)
                {
                    error = true;
                    MessageBox.Show("Enter a valid width parameter", "Invalid parameter");
                    txtW.Text = config.AppSettings.Settings["width"].Value;
                }

            }
            if (!string.IsNullOrWhiteSpace(textCh))
            {
                try
                {
                    App.AppSniffingManager.Channel = Byte.Parse(textCh);
                    config.AppSettings.Settings["channel"].Value = textCh;
                    config.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");
                }
                catch (Exception)
                {
                    error = true;
                    MessageBox.Show("Enter a valid channel parameter", "Invalid parameter");
                    txtCh.Text = config.AppSettings.Settings["channel"].Value;
                }

            }
            if (!string.IsNullOrWhiteSpace(textTim))
            {
                try
                {
                    App.AppSniffingManager.SniffingPeriod = UInt16.Parse(textTim);
                    config.AppSettings.Settings["timer"].Value = textTim;
                    config.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");
                }
                catch (Exception)
                {
                    error = true;
                    MessageBox.Show("Enter a valid timer parameter", "Invalid parameter");
                    txtTim.Text = config.AppSettings.Settings["timer"].Value;
                }

            }
            if (!string.IsNullOrWhiteSpace(textPor))
            {
                try
                {
                    App.AppSniffingManager.Port = UInt16.Parse(textPor);
                    config.AppSettings.Settings["port"].Value = textPor;
                    config.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");
                }
                catch (Exception)
                {
                    error = true;
                    MessageBox.Show("Enter a valid port parameter", "Invalid parameter");
                    txtPort.Text = config.AppSettings.Settings["port"].Value;
                }

            }

            if (!error)
            {
                this.Close();
            }


        }

        private void materialLabel2_Click(object sender, EventArgs e) {

        }

        private void txtW_TextChanged(object sender, EventArgs e) {

        }

        private void materialLabel1_Click(object sender, EventArgs e) {

        }
    }
}
