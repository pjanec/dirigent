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
            : base( "AppId '"+appIdTuple.MachineId+"."+appIdTuple.AppId+"' not found." )
        {
            this.appIdTuple = appIdTuple;
        }
    }

    public class NotALocalApp : Exception
    {
        public AppIdTuple appIdTuple;
        
        public NotALocalApp( AppIdTuple appIdTuple, string localMachineId )
            : base( "MachineId in '"+appIdTuple.MachineId+"."+appIdTuple.AppId+"' is not the one of this computer ("+localMachineId+")." )
        {
            this.appIdTuple = appIdTuple;
        }
    }

    public class UnknownAppInitDetectorType : Exception
    {
        public string initConditions;
        
        public UnknownAppInitDetectorType( string initConditions )
            : base( "Unknown init detector type '"+initConditions+"'." )
        {
            this.initConditions = initConditions;
        }
    }

    public class InvalidAppInitDetectorArguments : Exception
    {
        public string name;
        public string args;
        
        public InvalidAppInitDetectorArguments( string name, string args )
            : base( string.Format("Invalid init detector '{0}' arguments '{1}'.", name, args ) )
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

        public UnknownPlanName(string name)
            : base("Launch plan '" + name + "' was not found.")
        {
            this.name = name;
        }
    }

    public class AppStartFailureException : Exception
    {
        public AppIdTuple appIdTuple;
        public string reason;

        public AppStartFailureException(AppIdTuple appIdTuple, string reason, Exception innerEx=null)
            : base(
                string.Format( "Application '{0}' failed to start. Reason: {1}", appIdTuple, reason),
                innerEx )
        {
            this.appIdTuple = appIdTuple;
            this.reason = reason;
        }
    }

    // error, caused by some dirigent operation, coming from some remote dirigent agent;
    // to be reported to the user
    public class RemoteOperationErrorException : Exception
    {
        public string Requestor; // what agent requested the operation that caused the error
        public Dictionary<string, string> Attributes; // additional attribute pairs (name, value)

        public RemoteOperationErrorException(string requestor, string msg, Dictionary<string, string> attribs=null)
            : base(msg)
        {
            if (attribs != null)
            {
                this.Requestor = requestor;
                this.Attributes = attribs;
            }
        }
    }
}
