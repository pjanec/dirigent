using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Dirigent.Gui.WinForms
{
	/// <summary>
	/// Fires action associated with the latest balloon if the balloon is clicked before the timeout.
	/// Works only if there is max one baloon displayed at a time, otherwise unreliable
	///   - we can't determine what baloon was clicked so we can run the action of wrong baloon
	/// </summary>
	public class NotifyIconHandler
	{
		NotifyIcon _notifyIcon;
		DateTime _balloonClickTimeout = DateTime.MinValue; // last time when we should respond to baloon click event
		Action _onBalloonClicked; // what action to run when baloon is clicked in time while the balloon still shown

		public string Text
		{
			get { return _notifyIcon.Text; }
			set { _notifyIcon.Text = value; }
		}
		
		public NotifyIconHandler( NotifyIcon notifyIcon )
		{
			_notifyIcon = notifyIcon;
			_notifyIcon.BalloonTipClicked += OnBalloonTipClicked;
		}

		public void ShowBalloonTip( int timeout, string title, string text, ToolTipIcon icon, Action onClick=null )
		{
			_notifyIcon.ShowBalloonTip( timeout, title, text, icon );

			_onBalloonClicked = onClick;

			var clickEnabledTime = System.Math.Clamp( timeout, 5000, 10000 );
			_balloonClickTimeout = DateTime.Now.AddMilliseconds( clickEnabledTime );
		}

		private void OnBalloonTipClicked(Object sender, EventArgs e)
		{
			var now = DateTime.Now;
			if (now < _balloonClickTimeout)
			{
				_onBalloonClicked?.Invoke();
			}
		}

	}
}
