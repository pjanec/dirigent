using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using WinFormsSyntaxHighlighter;

namespace Dirigent.Gui.WinForms
{
	public partial class frmAppProperties : Form
	{
		AppDef _appDef;
		GuiCore _core;

		List<Scripts.BuiltIn.GetProcessWindows.WinInfo> _windows = new();

		public frmAppProperties( GuiCore core, AppDef appDef )
		{
			InitializeComponent();

			_core = core;
			_appDef = appDef;
			Text = $"[{appDef.Id}] Properties";
		}

		private void frmAppProperties_Load( object sender, EventArgs e )
		{
			InitAppDefPage();
			//Task.Run( InitProcessInfoPage );
			//Task.Run( InitWindowsPage );
		}

		void InitAppDefPage()
		{
			SetRichTextBoxJson( rtbAppDef, _appDef );
		}
		
		async void InitProcessInfoPage()
		{
			// starts a script on the remote machine that will return the process info
			// we show the info in the page as Rich Text
			var result = new Scripts.BuiltIn.GetProcessInfo.TResult();
			var rtb = rtbProcInfo;
			
			var appState = _core.Ctrl.GetAppState( _appDef.Id );
			if( appState is null ) return;
			if( !appState.Running )
			{
				RtbAction( rtb, () => rtb.Text = "App not running." );
			}
			else
			{
				RtbAction( rtb, () => rtb.Text = "Loading..." );
				try
				{
					result = await _core.ReflStates.ScriptReg.RunScriptAsync<Scripts.BuiltIn.GetProcessInfo.TArgs, Scripts.BuiltIn.GetProcessInfo.TResult>(
							_appDef.Id.MachineId, // run on machine where the app is running
							Scripts.BuiltIn.GetProcessInfo._Name,
							null,   // sourceCode
							new Scripts.BuiltIn.GetProcessInfo.TArgs() { PID = appState.PID },
							$"GetProcessInfo {_appDef.Id} PID={appState.PID}",
							out var instance
						);
				}
				catch( Exception e )
				{
					result.CommandLine = $"Error: {e.Message}";
				}

				// show the info in the page as Rich Text
				RtbAction( rtb, () => SetRichTextBoxJson( rtb, result ) );
			}

		}

		async void InitWindowsPage()
		{
			_windows.Clear();
			lbWindows.Invoke( () => lbWindows.Items.Clear() );

			var result = new Scripts.BuiltIn.GetProcessWindows.TResult();
			
			var appState = _core.Ctrl.GetAppState( _appDef.Id );
			if( appState is null ) return;
			if( !appState.Running )
			{
			}
			else
			{
				try
				{
					result = await _core.ReflStates.ScriptReg.RunScriptAsync<Scripts.BuiltIn.GetProcessWindows.TArgs, Scripts.BuiltIn.GetProcessWindows.TResult>(
							_appDef.Id.MachineId, // run on machine where the app is running
							Scripts.BuiltIn.GetProcessWindows._Name,
							null,   // sourceCode
							new Scripts.BuiltIn.GetProcessWindows.TArgs() { PID = appState.PID },
							$"GetProcessWindows {_appDef.Id} PID={appState.PID}",
							out var instance
						);
				}
				catch( Exception e )
				{
					int i=0;
				}

				_windows = (from x in result.Windows where (
					((x.Style & WinApi.WS_CHILD) == 0) &&
					((x.Style & WinApi.WS_DISABLED) == 0) &&
					(x.Style & WinApi.WS_CAPTION) != 0 &&
					true
				) select x).ToList();

				

				// show the info in the page as Rich Text
				lbWindows.Invoke( new Action( () => 
				{
					lbWindows.Items.Clear();
					foreach (var w in _windows)
					{
						lbWindows.Items.Add( w.Title );
					}
					lbWindows.SelectedIndex = 0;
				} ));
			}

		}

		// safe call to RichTextBox from another thread
		void RtbAction( RichTextBox rtb, Action action )
		{
			if( rtb.InvokeRequired )
			{
				rtb.Invoke( action );
			}
			else
			{
				action();
			}
		}
		
		void SetRichTextBoxJson<T>( RichTextBox rtb, T obj )
		{
			string jsonString = JsonSerializer.Serialize( obj, new JsonSerializerOptions() { WriteIndented = true, IncludeFields = true } );
			rtb.Text = jsonString;
			var syntaxHighlighter = new SyntaxHighlighter( rtb );
			JsonPatternsPreset.ApplyTo( syntaxHighlighter );
			syntaxHighlighter.ReHighlight();
		}


		void SetXmlString( string xmlText )
		{
			rtbAppDef.Clear();

			XmlReaderSettings settings = new XmlReaderSettings();

			var stream = new MemoryStream(Encoding.UTF8.GetBytes(xmlText ?? ""));
			int indent = 0;
			using (XmlReader reader = XmlReader.Create( stream, settings))
			{
				while(reader.Read())
				{
					switch (reader.NodeType)
					{
						case XmlNodeType.Element:
							rtbAppDef.SelectionColor = Color.Blue;
							rtbAppDef.AppendText("<");
							rtbAppDef.SelectionColor = Color.Brown;
							rtbAppDef.AppendText(reader.Name);
							rtbAppDef.SelectionColor = Color.Blue;
							rtbAppDef.AppendText(">");
							indent++;
	                        break;

						case XmlNodeType.Text:
							rtbAppDef.SelectionColor = Color.Blue;
							rtbAppDef.AppendText(reader.Value);
							break;

						case XmlNodeType.EndElement:
							indent--;
							rtbAppDef.SelectionColor = Color.Blue;
							rtbAppDef.AppendText("</");
							rtbAppDef.SelectionColor = Color.Brown;
							rtbAppDef.AppendText(reader.Name);
							rtbAppDef.SelectionColor = Color.Blue;
							rtbAppDef.AppendText(">");
							rtbAppDef.AppendText("\n");
							break;

						case XmlNodeType.Attribute:
							rtbAppDef.SelectionColor = Color.Brown;
							rtbAppDef.AppendText(reader.Name);
							rtbAppDef.AppendText(" = ");
							rtbAppDef.AppendText(reader.Value);
							rtbAppDef.AppendText("\n");
	                        break;

						default:
							Console.WriteLine("Other node {0} with value {1}",
											reader.NodeType, reader.Value);
							break;
					}
				}
			}
		}

		void SetSelectedWinStyle( EWindowStyle style )
		{
			var idx = lbWindows.SelectedIndex;
			if (idx >= 0)
			{
				var w = _windows[idx];
				_core.Ctrl.Send( new Net.SetWindowStyleMessage( _appDef.Id, style, w.Handle ) );
			}
		}
		

		private void btnWindowsShow_Click( object sender, EventArgs e )
		{
			SetSelectedWinStyle( EWindowStyle.Normal );
		}

		private void btnWindowsHide_Click( object sender, EventArgs e )
		{
			SetSelectedWinStyle( EWindowStyle.Hidden );
		}

		private void btnWindowsMaximize_Click( object sender, EventArgs e )
		{
			SetSelectedWinStyle( EWindowStyle.Maximized );
		}

		private void btnWindowsMinimize_Click( object sender, EventArgs e )
		{
			SetSelectedWinStyle( EWindowStyle.Minimized );
		}
		private void btnWindowsRefresh_Click( object sender, EventArgs e )
		{
			Task.Run( InitWindowsPage );
		}

		private void tabControl1_Selecting( object sender, TabControlCancelEventArgs e )
		{
			if (tabControl1.SelectedTab == pageWindows)
			{
				InitWindowsPage();
			}
			else
			if (tabControl1.SelectedTab == pageProcessInfo)
			{
				InitProcessInfoPage();
			}
			else
			if (tabControl1.SelectedTab == pageAppDef)
			{
				InitAppDefPage();
			}
		}
	}
}
