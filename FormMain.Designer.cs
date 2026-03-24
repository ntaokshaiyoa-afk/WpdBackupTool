namespace WpdDemo
{
    partial class FormMain
    {
        /// <summary>
        /// 必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージド リソースを破棄する場合は true を指定し、その他の場合は false を指定します。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows フォーム デザイナーで生成されたコード

        /// <summary>
        /// デザイナー サポートに必要なメソッドです。このメソッドの内容を
        /// コード エディターで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.saveFileDialog1 = new System.Windows.Forms.SaveFileDialog();
            this.button1 = new System.Windows.Forms.Button();
            this.comboBox_device = new System.Windows.Forms.ComboBox();
            this.listView1 = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader4 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.textBox_downloadRoot = new System.Windows.Forms.TextBox();
            this.Btn_SelectDownloadRootFolder = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(706, 13);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 1;
            this.button1.Text = "download";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // comboBox_device
            // 
            this.comboBox_device.FormattingEnabled = true;
            this.comboBox_device.Location = new System.Drawing.Point(12, 13);
            this.comboBox_device.Name = "comboBox_device";
            this.comboBox_device.Size = new System.Drawing.Size(688, 20);
            this.comboBox_device.TabIndex = 3;
            this.comboBox_device.SelectedIndexChanged += new System.EventHandler(this.comboBox_device_SelectedIndexChanged);
            // 
            // listView1
            // 
            this.listView1.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2,
            this.columnHeader3,
            this.columnHeader4});
            this.listView1.HideSelection = false;
            this.listView1.Location = new System.Drawing.Point(13, 75);
            this.listView1.Name = "listView1";
            this.listView1.Size = new System.Drawing.Size(687, 363);
            this.listView1.TabIndex = 4;
            this.listView1.UseCompatibleStateImageBehavior = false;
            this.listView1.View = System.Windows.Forms.View.Details;
            this.listView1.DoubleClick += new System.EventHandler(this.listView1_DoubleClick);
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "name";
            this.columnHeader1.Width = 200;
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "種類";
            // 
            // columnHeader3
            // 
            this.columnHeader3.Text = "ID";
            // 
            // columnHeader4
            // 
            this.columnHeader4.Text = "Size";
            // 
            // textBox_downloadRoot
            // 
            this.textBox_downloadRoot.Location = new System.Drawing.Point(13, 40);
            this.textBox_downloadRoot.Name = "textBox_downloadRoot";
            this.textBox_downloadRoot.Size = new System.Drawing.Size(687, 19);
            this.textBox_downloadRoot.TabIndex = 5;
            // 
            // Btn_SelectDownloadRootFolder
            // 
            this.Btn_SelectDownloadRootFolder.Location = new System.Drawing.Point(706, 40);
            this.Btn_SelectDownloadRootFolder.Name = "Btn_SelectDownloadRootFolder";
            this.Btn_SelectDownloadRootFolder.Size = new System.Drawing.Size(33, 23);
            this.Btn_SelectDownloadRootFolder.TabIndex = 6;
            this.Btn_SelectDownloadRootFolder.Text = "...";
            this.Btn_SelectDownloadRootFolder.UseVisualStyleBackColor = true;
            this.Btn_SelectDownloadRootFolder.Click += new System.EventHandler(this.Btn_SelectDownloadRootFolder_Click);
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(787, 450);
            this.Controls.Add(this.Btn_SelectDownloadRootFolder);
            this.Controls.Add(this.textBox_downloadRoot);
            this.Controls.Add(this.listView1);
            this.Controls.Add(this.comboBox_device);
            this.Controls.Add(this.button1);
            this.Name = "FormMain";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.FormMain_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.SaveFileDialog saveFileDialog1;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.ComboBox comboBox_device;
        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        private System.Windows.Forms.ColumnHeader columnHeader3;
        private System.Windows.Forms.ColumnHeader columnHeader4;
        private System.Windows.Forms.TextBox textBox_downloadRoot;
        private System.Windows.Forms.Button Btn_SelectDownloadRootFolder;
    }
}

