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

}
