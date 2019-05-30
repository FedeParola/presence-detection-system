using System;
using System.Collections.Generic;
using System.Configuration;
using System.Windows.Forms;
using MaterialSkin;
using PDSApp.SniffingManagement;

namespace PDSApp.GUI {
    public partial class ESPconfiguration: MaterialSkin.Controls.MaterialForm
    {
        MaterialSkin.MaterialSkinManager skinManager;
        string btnName;
        public ESPconfiguration(string name)
        {
            InitializeComponent();
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
            System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath);


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

                    App.AppSniffingManager.AddSniffer(new Sniffer(textip, new PDSApp.SniffingManagement.Trilateration.Point(Int32.Parse(textX), Int32.Parse(textY))));

                    this.Close();

                }
            }
        }

        //Posso fare funzione che ritorna qualcosa. 
    }
}
