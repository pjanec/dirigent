using System;
using System.Xml.Linq;

namespace Dirigent
{
	/// <summary>
	/// Holds an app "not initialized" until the master has answered the dirigent command it sent.
	/// </summary>
	/// <remarks>
	/// What makes a plan wait for something Dirigent does rather than for a process:
	/// `&lt;App ExeFullPath="[dirigent.command]" InitCondition="cliresponse ok"&gt;`. A plan launches an
	/// app once the ones it depends on are initialized, so a step that stays uninitialized until its
	/// command is over is a step the rest of the plan waits for.
	///
	/// It gates on the answer rather than on an exit code because such an app has no process and
	/// therefore no exit code to speak of - it counts as exited the moment it is asked, with code 0,
	/// which is why `exitcode 0` on such a step initializes it immediately whatever the command did.
	///
	/// The outcome itself is collected by the launcher that sent the request, not here: an answer
	/// that arrives before this detector's first tick would otherwise be missed.
	/// </remarks>
	public class CliResponseInitDetector : IAppInitializedDetector
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public IAppWatcher.EFlags Flags => IAppWatcher.EFlags.ClearOnLaunch;
		public bool ShallBeRemoved { get; set; }
		public LocalApp App => _app;

		/// <summary>Whether a failed command initializes the app as well - "any" rather than "ok".</summary>
		private bool _anyAnswerWillDo;

		private LocalApp _app;
		private AppState _appState;
		private bool _failureLogged;

		//<cliresponse>ok</cliresponse>
		public CliResponseInitDetector( LocalApp app, XElement xml )
		{
			_app = app;
			_appState = app.AppState;

			var value = xml.Value.Trim();

			if( string.Equals( value, InitConditions.Any, StringComparison.OrdinalIgnoreCase ) )
			{
				_anyAnswerWillDo = true;
			}
			else if( string.Equals( value, InitConditions.Ok, StringComparison.OrdinalIgnoreCase ) )
			{
				_anyAnswerWillDo = false;
			}
			else
			{
				// deliberately no default: "wait for it" and "wait for it to succeed" are different
				// enough decisions that a config has to make one of them out loud
				throw new InvalidAppInitDetectorArguments( Name,
					$"'{value}' - expected '{InitConditions.Ok}' or '{InitConditions.Any}'" );
			}

			_appState.Initialized = false; // until the answer arrives
		}

		static public string Name { get { return InitConditions.CliResponse; } }

		static public IAppInitializedDetector create( LocalApp app, XElement xml )
		{
			return new CliResponseInitDetector( app, xml );
		}

		bool IsInitialized()
		{
			var tracker = _app.CmdTracker;
			if( tracker is null ) return false;

			switch( tracker.Outcome )
			{
				case DirigentCmdTracker.EOutcome.Ok:
					return true;

				case DirigentCmdTracker.EOutcome.Error:
					if( _anyAnswerWillDo ) return true;

					if( !_failureLogged )
					{
						_failureLogged = true;
						log.Error( $"CliResponseInitDetector: {_app.Id} stays uninitialized - its command"
								+ $" failed: {tracker.ErrorText}" );
					}
					return false;

				default:
					return false; // still waiting for the answer
			}
		}

		bool IAppInitializedDetector.IsInitialized => IsInitialized();

		void IAppWatcher.Tick()
		{
			if( IsInitialized() )
			{
				_appState.Initialized = true;
				ShallBeRemoved = true;
			}
		}
	}
}
