using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using IPFBrowser.FileFormats.IPF;

namespace IPFBrowser
{
	/// <summary>
	/// Represents a file selected for extraction.
	/// </summary>
	public class SelectedFileInfo
	{
		public string FileTag { get; set; }
		public string FileName { get; set; }
		// sourceIpfIndex: -1 for main, 0+ for additional IPF index
		public int SourceIpfIndex { get; set; }
	}

	/// <summary>
	/// Dialog for selecting files and IPFs to extract with multi-select support.
	/// </summary>
	public partial class FrmExtractDialog : Form
	{
		public List<SelectedFileInfo> SelectedFiles { get; private set; }

		private Dictionary<string, IpfFile> _mainFiles;
		private Dictionary<string, IpfFile> _additionalFiles;
		private List<Ipf> _additionalIpfs;
		private Ipf _openedIpf;

		public FrmExtractDialog(Ipf openedIpf, Dictionary<string, IpfFile> mainFiles, 
			List<Ipf> additionalIpfs, Dictionary<string, IpfFile> additionalFiles)
		{
			InitializeComponent();
			_openedIpf = openedIpf;
			_mainFiles = mainFiles ?? new Dictionary<string, IpfFile>();
			_additionalIpfs = additionalIpfs ?? new List<Ipf>();
			_additionalFiles = additionalFiles ?? new Dictionary<string, IpfFile>();
			SelectedFiles = new List<SelectedFileInfo>();
			
			// Add keyboard handler for Ctrl+A
			fileListView.KeyDown += FileListView_KeyDown;
			
			// Populate after form is shown to prevent UI freezing
			this.Load += FrmExtractDialog_Load;
		}

		private void FrmExtractDialog_Load(object sender, EventArgs e)
		{
			// Disable extract button until files are loaded
			btnOk.Enabled = false;
			lblInstructions.Text = "Loading file list, please wait...";
			
			// Use async population to prevent freezing
			System.Threading.ThreadPool.QueueUserWorkItem(_ => 
			{
				// Small delay to ensure form is fully rendered
				System.Threading.Thread.Sleep(50);
				this.Invoke((Action)(() =>
				{
					PopulateFileList();
					btnOk.Enabled = true;
					var totalFiles = fileListView.Items.Count;
					lblInstructions.Text = $"Select files to extract ({totalFiles:N0} files available). Use checkboxes to select files, or Ctrl+A to select all.";
				}));
			});
		}

		private void PopulateFileList()
		{
			fileListView.BeginUpdate();
			try
			{
				fileListView.Items.Clear();
				fileListView.CheckBoxes = true;
				fileListView.View = View.Details;
				fileListView.Columns.Clear();
				fileListView.Columns.Add("File Name", 300);
				fileListView.Columns.Add("Source", 150);

				// Add main IPF files
				if (_mainFiles.Count > 0)
				{
					var ipfName = _openedIpf != null ? Path.GetFileName(_openedIpf.FilePath) : "Main IPF";
					var group = new ListViewGroup(ipfName, ipfName);
					fileListView.Groups.Add(group);

					foreach (var kvp in _mainFiles.OrderBy(f => f.Key))
					{
						var item = new ListViewItem(Path.GetFileName(kvp.Key), group)
						{
							Tag = new SelectedFileInfo { FileTag = kvp.Key, FileName = kvp.Key, SourceIpfIndex = -1 },
							Checked = false
						};
						item.SubItems.Add("Main IPF");
						fileListView.Items.Add(item);
					}
				}

				// Add additional IPF files
				foreach (var ipf in _additionalIpfs)
				{
					var ipfIndex = _additionalIpfs.IndexOf(ipf);
					var group = new ListViewGroup($"IPF: {Path.GetFileName(ipf.FilePath)}", $"IPF: {Path.GetFileName(ipf.FilePath)}");
					fileListView.Groups.Add(group);

					foreach (var file in ipf.Files)
					{
						var fileTag = $"IPF{(ipfIndex + 1)}:{file.FullPath}";
						var item = new ListViewItem(Path.GetFileName(file.FullPath), group)
						{
							Tag = new SelectedFileInfo { FileTag = fileTag, FileName = file.FullPath, SourceIpfIndex = ipfIndex },
							Checked = false
						};
						item.SubItems.Add(Path.GetFileName(ipf.FilePath));
						fileListView.Items.Add(item);
					}
				}
			}
			finally
			{
				fileListView.EndUpdate();
			}
		}

		private void BtnSelectAll_Click(object sender, EventArgs e)
		{
			fileListView.BeginUpdate();
			try
			{
				foreach (ListViewItem item in fileListView.Items)
					item.Checked = true;
			}
			finally
			{
				fileListView.EndUpdate();
			}
		}

		private void BtnDeselectAll_Click(object sender, EventArgs e)
		{
			fileListView.BeginUpdate();
			try
			{
				foreach (ListViewItem item in fileListView.Items)
					item.Checked = false;
			}
			finally
			{
				fileListView.EndUpdate();
			}
		}

		private void BtnOk_Click(object sender, EventArgs e)
		{
			SelectedFiles.Clear();
			foreach (ListViewItem item in fileListView.CheckedItems)
			{
				var data = (SelectedFileInfo)item.Tag;
				SelectedFiles.Add(new SelectedFileInfo 
				{ 
					FileTag = data.FileTag, 
					FileName = data.FileName, 
					SourceIpfIndex = data.SourceIpfIndex 
				});
			}
			DialogResult = DialogResult.OK;
			Close();
		}

		private void BtnCancel_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.Cancel;
			Close();
		}

		private void FileListView_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Control && e.KeyCode == Keys.A)
			{
				e.Handled = true;
				foreach (ListViewItem item in fileListView.Items)
					item.Checked = true;
			}
		}
	}
}
