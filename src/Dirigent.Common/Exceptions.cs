using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dirigent.Common
{
    public class UnknownAppIdException : Exception
    {
        public string appId;
        
        public UnknownAppIdException( string appId )
            : base( "AppId '"+appId+"' not found." )
        {
            this.appId = appId;
        }
    }
}
