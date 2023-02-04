using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Dirigent.Gui.WinForms
{
	class StatusBarSection
	{
		public string Id { get; private set; }	// "" = main section, "SSH" = ssh section
		ToolStripStatusLabel _label;
		public DateTime Expiration { get; set; }

		public StatusBarSection( string id, ToolStripStatusLabel label )
		{
			Id = id;
			_label = label;
		}

		public string Text
		{
			get
			{
				return _label.Text;
			}
			set
			{
				_label.Text = value;
			}
		}

	}

	public class StatusBarManager : Disposable
	{
		StatusStrip _statusStrip;
		Dictionary<string, StatusBarSection> _sections = new Dictionary<string, StatusBarSection>();

		public StatusBarManager( StatusStrip statusStrip )
		{
			_statusStrip = statusStrip;
			
			// use the design-time defined labels
			{
				var label = _statusStrip.Items[0] as ToolStripStatusLabel;
				var sect = new StatusBarSection("", label);
				_sections.Add( sect.Id, sect );
			}
			{
				var label = _statusStrip.Items[1] as ToolStripStatusLabel;
				var sect = new StatusBarSection("SSH", label);
				_sections.Add( sect.Id, sect );
			}

			AppMessenger.Instance.Register<AppMessages.StatusText>( OnStatusText );
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if( !disposing ) return;
			AppMessenger.Instance.Unregister<AppMessages.StatusText>( OnStatusText );
		}

		void OnStatusText( AppMessages.StatusText msg )
		{
			if( _sections.TryGetValue( msg.Section, out var sect ))
			{
				sect.Text = msg.Text;
				sect.Expiration = msg.TimeoutMsec <= 0 ? DateTime.MinValue : DateTime.Now + TimeSpan.FromMilliseconds( msg.TimeoutMsec );
				
				_statusStrip.Refresh();
			}
		}

		void CheckTimeouts()
		{
			foreach(var sect in _sections.Values)
			{
				if( sect.Expiration != DateTime.MinValue && sect.Expiration < DateTime.Now)
				{
					sect.Text = "";
					sect.Expiration = DateTime.MinValue;
					_statusStrip.Refresh();
				}
			}
		}
		
		public void Tick()
		{
			CheckTimeouts();
		}
	}
}
