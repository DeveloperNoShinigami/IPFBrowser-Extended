// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Windows.Forms;
using System.IO;

namespace IPFBrowser
{
	static class Program
	{
		private static string _errorLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ErrorLogs", $"Startup_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

		/// <summary>
		/// Der Haupteinstiegspunkt für die Anwendung.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			try
			{
				// Ensure ErrorLogs directory exists
				Directory.CreateDirectory(Path.GetDirectoryName(_errorLogPath));

				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);
				
				// Global exception handler for unhandled exceptions
				AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
				{
					LogError("UNHANDLED EXCEPTION", e.ExceptionObject as Exception);
					MessageBox.Show($"A critical error occurred:\n\n{e.ExceptionObject}", "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				};

				// Handle UI thread exceptions
				Application.ThreadException += (sender, e) =>
				{
					LogError("THREAD EXCEPTION", e.Exception);
					MessageBox.Show($"An error occurred:\n\n{e.Exception.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				};

				Application.Run(new FrmMain(args));
			}
			catch (Exception ex)
			{
				LogError("MAIN STARTUP EXCEPTION", ex);
				MessageBox.Show($"Failed to start application:\n\n{ex.Message}\n\nSee ErrorLogs folder for details.", "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private static void LogError(string title, Exception ex)
		{
			try
			{
				string logContent = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}\n\n";
				if (ex != null)
				{
					logContent += $"Exception Type: {ex.GetType().FullName}\n";
					logContent += $"Message: {ex.Message}\n";
					logContent += $"Stack Trace:\n{ex.StackTrace}\n";
					
					if (ex.InnerException != null)
					{
						logContent += $"\nInner Exception:\n{ex.InnerException.Message}\n{ex.InnerException.StackTrace}\n";
					}
				}
				
				File.AppendAllText(_errorLogPath, logContent + "\n" + new string('-', 80) + "\n\n");
			}
			catch
			{
				// Silent fail if logging fails
			}
		}
	}
}
