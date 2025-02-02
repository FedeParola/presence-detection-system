﻿namespace PDSApp.GUI {
    partial class ConfigureParameters
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.buttonSavePrj = new MaterialSkin.Controls.MaterialRaisedButton();
            this.txtTim = new System.Windows.Forms.TextBox();
            this.txtCh = new System.Windows.Forms.TextBox();
            this.txtH = new System.Windows.Forms.TextBox();
            this.txtW = new System.Windows.Forms.TextBox();
            this.materialLabel4 = new MaterialSkin.Controls.MaterialLabel();
            this.materialLabel3 = new MaterialSkin.Controls.MaterialLabel();
            this.materialLabel2 = new MaterialSkin.Controls.MaterialLabel();
            this.materialLabel1 = new MaterialSkin.Controls.MaterialLabel();
            this.txtPort = new System.Windows.Forms.TextBox();
            this.materialLabel5 = new MaterialSkin.Controls.MaterialLabel();
            this.SuspendLayout();
            // 
            // buttonSavePrj
            // 
            this.buttonSavePrj.Depth = 0;
            this.buttonSavePrj.Location = new System.Drawing.Point(210, 301);
            this.buttonSavePrj.Margin = new System.Windows.Forms.Padding(2);
            this.buttonSavePrj.MouseState = MaterialSkin.MouseState.HOVER;
            this.buttonSavePrj.Name = "buttonSavePrj";
            this.buttonSavePrj.Primary = true;
            this.buttonSavePrj.Size = new System.Drawing.Size(135, 31);
            this.buttonSavePrj.TabIndex = 17;
            this.buttonSavePrj.Text = "Save";
            this.buttonSavePrj.UseVisualStyleBackColor = true;
            this.buttonSavePrj.Click += new System.EventHandler(this.buttonSavePrj_Click);
            // 
            // txtTim
            // 
            this.txtTim.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtTim.Location = new System.Drawing.Point(210, 205);
            this.txtTim.Margin = new System.Windows.Forms.Padding(2);
            this.txtTim.Name = "txtTim";
            this.txtTim.Size = new System.Drawing.Size(136, 26);
            this.txtTim.TabIndex = 16;
            // 
            // txtCh
            // 
            this.txtCh.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtCh.Location = new System.Drawing.Point(210, 160);
            this.txtCh.Margin = new System.Windows.Forms.Padding(2);
            this.txtCh.Name = "txtCh";
            this.txtCh.Size = new System.Drawing.Size(136, 26);
            this.txtCh.TabIndex = 15;
            // 
            // txtH
            // 
            this.txtH.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtH.Location = new System.Drawing.Point(210, 75);
            this.txtH.Margin = new System.Windows.Forms.Padding(2);
            this.txtH.Name = "txtH";
            this.txtH.Size = new System.Drawing.Size(136, 26);
            this.txtH.TabIndex = 14;
            // 
            // txtW
            // 
            this.txtW.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtW.Location = new System.Drawing.Point(209, 117);
            this.txtW.Margin = new System.Windows.Forms.Padding(2);
            this.txtW.Name = "txtW";
            this.txtW.Size = new System.Drawing.Size(136, 26);
            this.txtW.TabIndex = 13;
            this.txtW.TextChanged += new System.EventHandler(this.txtW_TextChanged);
            // 
            // materialLabel4
            // 
            this.materialLabel4.AutoSize = true;
            this.materialLabel4.Depth = 0;
            this.materialLabel4.Font = new System.Drawing.Font("Roboto", 11F);
            this.materialLabel4.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(222)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.materialLabel4.Location = new System.Drawing.Point(52, 205);
            this.materialLabel4.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.materialLabel4.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel4.Name = "materialLabel4";
            this.materialLabel4.Size = new System.Drawing.Size(136, 19);
            this.materialLabel4.TabIndex = 12;
            this.materialLabel4.Text = "Sniffing period (s) :";
            // 
            // materialLabel3
            // 
            this.materialLabel3.AutoSize = true;
            this.materialLabel3.Depth = 0;
            this.materialLabel3.Font = new System.Drawing.Font("Roboto", 11F);
            this.materialLabel3.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(222)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.materialLabel3.Location = new System.Drawing.Point(52, 160);
            this.materialLabel3.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.materialLabel3.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel3.Name = "materialLabel3";
            this.materialLabel3.Size = new System.Drawing.Size(102, 19);
            this.materialLabel3.TabIndex = 11;
            this.materialLabel3.Text = "WiFi channel :";
            // 
            // materialLabel2
            // 
            this.materialLabel2.AutoSize = true;
            this.materialLabel2.Depth = 0;
            this.materialLabel2.Font = new System.Drawing.Font("Roboto", 11F);
            this.materialLabel2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(222)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.materialLabel2.Location = new System.Drawing.Point(52, 79);
            this.materialLabel2.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.materialLabel2.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel2.Name = "materialLabel2";
            this.materialLabel2.Size = new System.Drawing.Size(123, 19);
            this.materialLabel2.TabIndex = 10;
            this.materialLabel2.Text = "Room length (x) :";
            this.materialLabel2.Click += new System.EventHandler(this.materialLabel2_Click);
            // 
            // materialLabel1
            // 
            this.materialLabel1.AutoSize = true;
            this.materialLabel1.Depth = 0;
            this.materialLabel1.Font = new System.Drawing.Font("Roboto", 11F);
            this.materialLabel1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(222)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.materialLabel1.Location = new System.Drawing.Point(52, 121);
            this.materialLabel1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.materialLabel1.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel1.Name = "materialLabel1";
            this.materialLabel1.Size = new System.Drawing.Size(118, 19);
            this.materialLabel1.TabIndex = 9;
            this.materialLabel1.Text = "Room width (y) :";
            this.materialLabel1.Click += new System.EventHandler(this.materialLabel1_Click);
            // 
            // txtPort
            // 
            this.txtPort.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtPort.Location = new System.Drawing.Point(210, 249);
            this.txtPort.Margin = new System.Windows.Forms.Padding(2);
            this.txtPort.Name = "txtPort";
            this.txtPort.Size = new System.Drawing.Size(136, 26);
            this.txtPort.TabIndex = 19;
            // 
            // materialLabel5
            // 
            this.materialLabel5.AutoSize = true;
            this.materialLabel5.Depth = 0;
            this.materialLabel5.Font = new System.Drawing.Font("Roboto", 11F);
            this.materialLabel5.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(222)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.materialLabel5.Location = new System.Drawing.Point(52, 249);
            this.materialLabel5.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.materialLabel5.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel5.Name = "materialLabel5";
            this.materialLabel5.Size = new System.Drawing.Size(75, 19);
            this.materialLabel5.TabIndex = 18;
            this.materialLabel5.Text = "ESP port :";
            // 
            // ConfigureParameters
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(501, 350);
            this.Controls.Add(this.txtPort);
            this.Controls.Add(this.materialLabel5);
            this.Controls.Add(this.buttonSavePrj);
            this.Controls.Add(this.txtTim);
            this.Controls.Add(this.txtCh);
            this.Controls.Add(this.txtH);
            this.Controls.Add(this.txtW);
            this.Controls.Add(this.materialLabel4);
            this.Controls.Add(this.materialLabel3);
            this.Controls.Add(this.materialLabel2);
            this.Controls.Add(this.materialLabel1);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ConfigureParameters";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Configure Project Parameters";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private MaterialSkin.Controls.MaterialRaisedButton buttonSavePrj;
        private System.Windows.Forms.TextBox txtTim;
        private System.Windows.Forms.TextBox txtCh;
        private System.Windows.Forms.TextBox txtH;
        private System.Windows.Forms.TextBox txtW;
        private MaterialSkin.Controls.MaterialLabel materialLabel4;
        private MaterialSkin.Controls.MaterialLabel materialLabel3;
        private MaterialSkin.Controls.MaterialLabel materialLabel2;
        private MaterialSkin.Controls.MaterialLabel materialLabel1;
        private System.Windows.Forms.TextBox txtPort;
        private MaterialSkin.Controls.MaterialLabel materialLabel5;
    }
}