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
using System.Xml;
using MaterialSkin;

namespace ESP32_Application
{
    public partial class ESPconfiguration: MaterialSkin.Controls.MaterialForm
    {
        MaterialSkin.MaterialSkinManager skinManager;
        ESPdatiGlobali globalData;
        List<ESPmomentanea> ESPcollection;
        string btnName;
        public ESPconfiguration(ESPdatiGlobali globalData, List<ESPmomentanea> ESPcollection, string name)
        {
            InitializeComponent();
            this.globalData = globalData;
            this.ESPcollection = ESPcollection; ;
            this.btnName = name;
            skinManager = MaterialSkinManager.Instance;
            skinManager.AddFormToManage(this);
            skinManager.Theme = MaterialSkinManager.Themes.LIGHT;
            skinManager.ColorScheme = new ColorScheme(Primary.BlueGrey700, Primary.Grey600, Primary.Grey900, Accent.LightBlue200, TextShade.WHITE);
                
    }

        private void materialRaisedButton1_Click(object sender, EventArgs e)
        {
            String textip = txtIP.Text;
            String textX = txtX.Text;
            String textY = txtY.Text;
            int flag = 0; int i;
            Configuration config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath);


            //Coming from "add new ESP"
            if (btnName.Equals("buttonNew"))
            {
                if (string.IsNullOrWhiteSpace(textip) ||  string.IsNullOrWhiteSpace(textX) || string.IsNullOrWhiteSpace(textY))
                {
                    MessageBox.Show("Error : Enter all data to continue");
                    return;
                }
                else
                {
                    if (!MainWindow.CheckIP(textip))
                    {
                        MessageBox.Show("Error : invalid IP address");
                        return;
                    }

                    //Nota : non aprire il file app.config, non riporta le modifiche in fase di sviluppo. 
                    //aggiungo una ESP con i valori delle textBox
                    string value = textX + ";" + textY;
                    config.AppSettings.Settings.Add(textip, value);
                    config.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");

                    globalData.EspNumber = globalData.EspNumber + 1;
                    ESPmomentanea esp = new ESPmomentanea(MainWindow.GenerateID(textip), textip, "attivo", Int32.Parse(textY), Int32.Parse(textY));
                    ESPcollection.Add(esp);

                    this.Close();

                }
            }
            else //Coming from Configuration of a single ESP module
            {
                for (i = 0; i < ESPcollection.Count; i++)
                {
                    if (ESPcollection[i].Id.Equals(btnName))
                    {
                        break;
                    }
                }
                string[] positions = ConfigurationManager.AppSettings[ESPcollection[i].Ipadd].Split(";");

                if (!string.IsNullOrWhiteSpace(textip))
                {
                    if (!MainWindow.CheckIP(textip))
                    {
                        MessageBox.Show("Error : invalid IP address");
                        return;
                    }

                    string values = ConfigurationManager.AppSettings[ESPcollection[i].Ipadd];
                    config.AppSettings.Settings.Remove(ESPcollection[i].Ipadd);
                    config.AppSettings.Settings.Add(textip, values);
                    config.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");
                    ESPcollection[i].Ipadd = textip;
                    ESPcollection[i].Id = MainWindow.GenerateID(textip);
                    flag = 1;
                }
                if (!string.IsNullOrWhiteSpace(textX))
                {
                    ESPcollection[i].X = Int32.Parse(textX);
                    string newValue = textX + ";" + positions[1];
                    config.AppSettings.Settings[ESPcollection[i].Ipadd].Value = newValue;
                    config.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");
                    flag = 1;
                }
                if (!string.IsNullOrWhiteSpace(textY))
                {
                    ESPcollection[i].Y = Int32.Parse(textY);
                    string newValue = positions[0] + ";" + textY;
                    config.AppSettings.Settings[ESPcollection[i].Ipadd].Value = textY;
                    config.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");
                    flag = 1;
                }

                if (flag == 0)
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

        //Posso fare funzione che ritorna qualcosa. 
    }
}
