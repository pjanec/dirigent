using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;

namespace Dirigent.Gui
{
	public class ErrorRenderer
	{
		private string _uniqueUiId = Guid.NewGuid().ToString();
		private ImGuiWindow _wnd;
		
		private record Error( string Message, DateTime ExpirationTime );
		private List<Error> _errors = new List<Error>();
		
		public ErrorRenderer( ImGuiWindow wnd )
		{
			_wnd = wnd;
		}

		public void DrawUI()
		{
			ImGui.PushID(_uniqueUiId);

			// show non-expired errors
			var now = DateTime.Now;
			var toShow = from e in _errors where now < e.ExpirationTime select e;
			
			ImGui.PushStyleColor( ImGuiCol.Text, ImGuiColors.Red );
			foreach( var e in toShow )
			{
				ImGui.TextWrapped( e.Message.Replace( "%", "%%" ) );
				ImGui.Separator();
			}
			ImGui.PopStyleColor();

			ImGui.PopID();

			// remove expired
			_errors.RemoveAll( x => now >= x.ExpirationTime );
		}

		public void AddErrorMessage( string text, double expirationSecs=2.0 )
		{
			var expiration = DateTime.Now + new TimeSpan(0,0,0,0,(int)(1000.0*expirationSecs));
			_errors.Add( new Error( text, expiration) );
		}



	}
}
