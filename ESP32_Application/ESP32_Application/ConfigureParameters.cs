using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MaterialSkin;

namespace ESP32_Application
{
    public partial class ConfigureParameters : MaterialSkin.Controls.MaterialForm
    {
        MaterialSkin.MaterialSkinManager skinManager;
        ESPdatiGlobali globalData;
        public ConfigureParameters(ESPdatiGlobali globalData)
        {
            this.globalData = globalData;
            InitializeComponent();
            skinManager = MaterialSkinManager.Instance;
            skinManager.AddFormToManage(this);
            skinManager.Theme = MaterialSkinManager.Themes.LIGHT;
            skinManager.ColorScheme = new ColorScheme(Primary.BlueGrey700, Primary.Grey600, Primary.Grey900, Accent.LightBlue200, TextShade.WHITE);

        }

        private void buttonSavePrj_Click(object sender, EventArgs e)
        {
            String textW = txtW.Text;
            String textH = txtH.Text;
            String textCh = txtCh.Text;
            String textTim = txtTim.Text;
            int flag = 0;
            Configuration config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath);

            if (!string.IsNullOrWhiteSpace(textW))
            {
                globalData.Width = Int32.Parse(textW);
                config.AppSettings.Settings["width"].Value = textW;
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
                flag = 1;
            }
            if (!string.IsNullOrWhiteSpace(textH))
            {
                globalData.Height = Int32.Parse(textH);
                config.AppSettings.Settings["height"].Value = textH;
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
                flag = 1; 
            }
            if (!string.IsNullOrWhiteSpace(textCh))
            {
                globalData.Channel = Int32.Parse(textCh);
                config.AppSettings.Settings["channel"].Value = textCh;
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
                flag = 1;
            }
            if (!string.IsNullOrWhiteSpace(textTim))
            {
                globalData.Timer = Int32.Parse(textTim);
                config.AppSettings.Settings["timer"].Value = textTim;
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
                flag = 1;
            }

            if(flag == 0)
            {
                this.Close();
                MessageBox.Show("No changes on parameters");
            }
            else
            {
                this.Close();
            }


        }
    }
}
