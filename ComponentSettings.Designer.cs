namespace LiveSplit.EscapeGoat2
{
    partial class ComponentSettings
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.deathsRun = new System.Windows.Forms.CheckBox();
            this.deathsSession = new System.Windows.Forms.CheckBox();
            this.deathsTotal = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // deathsRun
            // 
            this.deathsRun.AutoSize = true;
            this.deathsRun.Location = new System.Drawing.Point(9, 9);
            this.deathsRun.Name = "deathsRun";
            this.deathsRun.Size = new System.Drawing.Size(136, 21);
            this.deathsRun.TabIndex = 0;
            this.deathsRun.Text = "Show run deaths";
            this.deathsRun.UseVisualStyleBackColor = true;
            // 
            // deathsSession
            // 
            this.deathsSession.AutoSize = true;
            this.deathsSession.Location = new System.Drawing.Point(9, 37);
            this.deathsSession.Name = "deathsSession";
            this.deathsSession.Size = new System.Drawing.Size(163, 21);
            this.deathsSession.TabIndex = 1;
            this.deathsSession.Text = "Show session deaths";
            this.deathsSession.UseVisualStyleBackColor = true;
            // 
            // deathsTotal
            // 
            this.deathsTotal.AutoSize = true;
            this.deathsTotal.Location = new System.Drawing.Point(9, 65);
            this.deathsTotal.Name = "deathsTotal";
            this.deathsTotal.Size = new System.Drawing.Size(142, 21);
            this.deathsTotal.TabIndex = 2;
            this.deathsTotal.Text = "Show total deaths";
            this.deathsTotal.UseVisualStyleBackColor = true;
            // 
            // Settings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.deathsTotal);
            this.Controls.Add(this.deathsSession);
            this.Controls.Add(this.deathsRun);
            this.Name = "Settings";
            this.Padding = new System.Windows.Forms.Padding(9);
            this.Size = new System.Drawing.Size(184, 98);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox deathsRun;
        private System.Windows.Forms.CheckBox deathsSession;
        private System.Windows.Forms.CheckBox deathsTotal;
    }
}
