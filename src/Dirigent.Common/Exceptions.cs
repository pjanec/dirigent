using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dirigent.Common
{
	public class UnknownAppIdException : Exception
	{
		public AppIdTuple appIdTuple;

		public UnknownAppIdException( AppIdTuple appIdTuple )
			: base( "AppId '" + appIdTuple.MachineId + "." + appIdTuple.AppId + "' not found." )
		{
			this.appIdTuple = appIdTuple;
		}
	}

	public class UnknownAppInPlanException : Exception
	{
		public AppIdTuple id;
		public string planName;

		public UnknownAppInPlanException( AppIdTuple id, string planName )
			: base( $"{id} not found in plan {planName}" )
		{
			this.id = id;
			this.planName = planName;
		}
	}

	public class NotALocalApp : Exception
	{
		public AppIdTuple id;

		public NotALocalApp( AppIdTuple id, string localMachineId )
			: base( String.Format( "App '{0}' not defined for machine '{1}'", id, localMachineId ) )
		{
			this.id = id;
		}
	}

	public class UnknownAppWatcherType : Exception
	{
		public string definitionString;

		public UnknownAppWatcherType( string definitionString )
			: base( "Unknown watcher type '" + definitionString + "'." )
		{
			this.definitionString = definitionString;
		}
	}

	public class InvalidAppConfig : Exception
	{
		public AppIdTuple id;
		public string msg;

		public InvalidAppConfig( AppIdTuple id, string msg )
			: base( string.Format( "Invalid app '{0}' config: '{1}'.", id, msg ) )
		{
			this.id = id;
			this.msg = msg;
		}
	}

	public class InvalidAppWatcherArguments : Exception
	{
		public string name;
		public string args;

		public InvalidAppWatcherArguments( string name, string args )
			: base( string.Format( "Invalid app watcher '{0}' arguments '{1}'.", name, args ) )
		{
			this.name = name;
			this.args = args;
		}
	}

	public class UnknownAppInitDetectorType : Exception
	{
		public string initConditions;

		public UnknownAppInitDetectorType( string initConditions )
			: base( "Unknown init detector type '" + initConditions + "'." )
		{
			this.initConditions = initConditions;
		}
	}

	public class InvalidAppInitDetectorArguments : Exception
	{
		public string name;
		public string args;

		public InvalidAppInitDetectorArguments( string name, string args )
			: base( string.Format( "Invalid init detector '{0}' arguments '{1}'.", name, args ) )
		{
			this.name = name;
			this.args = args;
		}
	}

	public class ConfigurationErrorException : Exception
	{
		public ConfigurationErrorException( string msg )
			: base( msg )
		{
		}
	}

	public class UnknownPlanName : Exception
	{
		public string name;

		public UnknownPlanName( string name )
			: base( "Launch plan '" + name + "' was not found." )
		{
			this.name = name;
		}
	}

	public class AppStartFailureException : Exception
	{
		public AppIdTuple id;
		public string reason;

		public AppStartFailureException( AppIdTuple id, string reason, Exception? innerEx = null )
			: base(
				  string.Format( "Application '{0}' failed to start. Reason: {1}", id, reason ),
				  innerEx )
		{
			this.id = id;
			this.reason = reason;
		}
	}

	// error, caused by some dirigent operation, coming from some remote dirigent agent;
	// to be reported to the user
	public class RemoteOperationErrorException : Exception
	{
		public string? Requestor; // what agent requested the operation that caused the error (null = not an agent)
		public Dictionary<string, string>? Attributes; // additional attribute pairs (name, value)

		public RemoteOperationErrorException( string? requestor, string msg, Dictionary<string, string>? attribs = null )
			: base( msg )
		{
			this.Requestor = requestor;
			this.Attributes = attribs;
		}
	}
}
