using System;
using System.Linq;
using System.Text;

namespace Dirigent.TestBed
{
	/// <summary>
	/// Log capture for failure reports. log4net configuration is process-global, so exactly one
	/// appender is installed for the whole test run.
	/// </summary>
	public static class Diagnostics
	{
		static readonly object _lock = new();
		static log4net.Appender.MemoryAppender? _appender;

		public static void EnsureLogCapture()
		{
			lock( _lock )
			{
				if( _appender is not null ) return;

				_appender = new log4net.Appender.MemoryAppender()
				{
					Name = "DirigentTestBedMemory",
					Threshold = log4net.Core.Level.Debug,
				};
				_appender.ActivateOptions();

				log4net.Config.BasicConfigurator.Configure( _appender );
			}
		}

		/// <summary>The tail of what Dirigent logged, which is usually where the real reason is.</summary>
		public static string RecentLog( int lines )
		{
			lock( _lock )
			{
				if( _appender is null ) return string.Empty;

				var events = _appender.GetEvents();
				if( events.Length == 0 ) return string.Empty;

				var sb = new StringBuilder();
				sb.AppendLine( $"last {Math.Min( lines, events.Length )} of {events.Length} log events:" );
				foreach( var e in events.Skip( Math.Max( 0, events.Length - lines ) ) )
				{
					sb.AppendLine( $"    {e.Level.Name,-5} {e.LoggerName.Split( '.' ).Last(),-24} {e.RenderedMessage}" );
					if( e.ExceptionObject is not null )
						sb.AppendLine( $"          {e.ExceptionObject.GetType().Name}: {e.ExceptionObject.Message}" );
				}
				return sb.ToString();
			}
		}

		/// <summary>Drops the captured events so one test's failure report is not another's.</summary>
		public static void ClearLog()
		{
			lock( _lock ) _appender?.Clear();
		}
	}
}
