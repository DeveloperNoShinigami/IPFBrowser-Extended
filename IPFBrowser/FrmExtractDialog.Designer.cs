namespace IPFBrowser
{
	partial class FrmExtractDialog
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
			this.fileListView = new System.Windows.Forms.ListView();
			this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.btnSelectAll = new System.Windows.Forms.Button();
			this.btnDeselectAll = new System.Windows.Forms.Button();
			this.btnOk = new System.Windows.Forms.Button();
			this.btnCancel = new System.Windows.Forms.Button();
			this.lblInstructions = new System.Windows.Forms.Label();
			this.SuspendLayout();
			// 
			// fileListView
			// 
			this.fileListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.fileListView.CheckBoxes = true;
			this.fileListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2});
			this.fileListView.FullRowSelect = true;
			this.fileListView.HideSelection = false;
			this.fileListView.Location = new System.Drawing.Point(12, 38);
			this.fileListView.MultiSelect = true;
			this.fileListView.Name = "fileListView";
			this.fileListView.Size = new System.Drawing.Size(760, 450);
			this.fileListView.TabIndex = 0;
			this.fileListView.UseCompatibleStateImageBehavior = false;
			this.fileListView.View = System.Windows.Forms.View.Details;
			// 
			// columnHeader1
			// 
			this.columnHeader1.Text = "File Name";
			this.columnHeader1.Width = 550;
			// 
			// columnHeader2
			// 
			this.columnHeader2.Text = "Source IPF";
			this.columnHeader2.Width = 200;
			// 
			// btnSelectAll
			// 
			this.btnSelectAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnSelectAll.Location = new System.Drawing.Point(12, 494);
			this.btnSelectAll.Name = "btnSelectAll";
			this.btnSelectAll.Size = new System.Drawing.Size(100, 28);
			this.btnSelectAll.TabIndex = 1;
			this.btnSelectAll.Text = "Select All";
			this.btnSelectAll.UseVisualStyleBackColor = true;
			this.btnSelectAll.Click += new System.EventHandler(this.BtnSelectAll_Click);
			// 
			// btnDeselectAll
			// 
			this.btnDeselectAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnDeselectAll.Location = new System.Drawing.Point(118, 494);
			this.btnDeselectAll.Name = "btnDeselectAll";
			this.btnDeselectAll.Size = new System.Drawing.Size(100, 28);
			this.btnDeselectAll.TabIndex = 2;
			this.btnDeselectAll.Text = "Deselect All";
			this.btnDeselectAll.UseVisualStyleBackColor = true;
			this.btnDeselectAll.Click += new System.EventHandler(this.BtnDeselectAll_Click);
			// 
			// btnOk
			// 
			this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnOk.Location = new System.Drawing.Point(566, 494);
			this.btnOk.Name = "btnOk";
			this.btnOk.Size = new System.Drawing.Size(100, 28);
			this.btnOk.TabIndex = 3;
			this.btnOk.Text = "Extract";
			this.btnOk.UseVisualStyleBackColor = true;
			this.btnOk.Click += new System.EventHandler(this.BtnOk_Click);
			// 
			// btnCancel
			// 
			this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.btnCancel.Location = new System.Drawing.Point(672, 494);
			this.btnCancel.Name = "btnCancel";
			this.btnCancel.Size = new System.Drawing.Size(100, 28);
			this.btnCancel.TabIndex = 4;
			this.btnCancel.Text = "Cancel";
			this.btnCancel.UseVisualStyleBackColor = true;
			// 
			// lblInstructions
			// 
			this.lblInstructions.AutoSize = true;
			this.lblInstructions.Location = new System.Drawing.Point(12, 15);
			this.lblInstructions.Name = "lblInstructions";
			this.lblInstructions.Size = new System.Drawing.Size(540, 13);
			this.lblInstructions.TabIndex = 5;
			this.lblInstructions.Text = "Select files to extract. Use checkboxes to select files, or Ctrl+A to select all" +
    ". Files will be extracted to separate folders per IPF.";
			// 
			// FrmExtractDialog
			// 
			this.AcceptButton = this.btnOk;
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.CancelButton = this.btnCancel;
			this.ClientSize = new System.Drawing.Size(784, 534);
			this.Controls.Add(this.lblInstructions);
			this.Controls.Add(this.btnCancel);
			this.Controls.Add(this.btnOk);
			this.Controls.Add(this.btnDeselectAll);
			this.Controls.Add(this.btnSelectAll);
			this.Controls.Add(this.fileListView);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.MinimumSize = new System.Drawing.Size(600, 400);
			this.Name = "FrmExtractDialog";
			this.ShowIcon = false;
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Extract Files from IPF";
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.ListView fileListView;
		private System.Windows.Forms.ColumnHeader columnHeader1;
		private System.Windows.Forms.ColumnHeader columnHeader2;
		private System.Windows.Forms.Button btnSelectAll;
		private System.Windows.Forms.Button btnDeselectAll;
		private System.Windows.Forms.Button btnOk;
		private System.Windows.Forms.Button btnCancel;
		private System.Windows.Forms.Label lblInstructions;
	}
}
