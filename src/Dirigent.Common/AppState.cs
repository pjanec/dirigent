using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Dirigent.Common
{

    /// <summary>
    /// App status shared among all Dirigent participants.
    /// </summary>
    public class AppState
    {
        public bool WasLaunched;
        public bool Running;
        public bool Initialized;
    }


}
