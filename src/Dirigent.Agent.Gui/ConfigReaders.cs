using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Dirigent.Common;

namespace Dirigent.Agent.Gui
{
    public class ConfigReaders
    {
        static public SharedConfig loadSharedConfig()
        {
            SharedXmlConfigReader cr = new SharedXmlConfigReader();
            string cfgFileName = Path.GetFullPath("../../../../data/SharedConfig.xml");
            try
            {
                return cr.Load(File.OpenText(cfgFileName));
            }
            catch (Exception ex)
            {
                ExceptionDialog.showException(
                    ex,
                    "Configuration Load Error",
                    string.Format("Failed to read configuration from file '{0}'.", cfgFileName)
                );
            }
            return null;
        }

    //    /// <summary>
    //    /// Returns none if config failed to load.
    //    /// </summary>
    //    /// <returns></returns>
    //    static public LocalConfig loadLocalConfig()
    //    {
    //        LocalXmlConfigReader cr = new LocalXmlConfigReader();
    //        string cfgFileName = Path.GetFullPath("../../../../data/LocalConfig.xml");
    //        if (!File.Exists(cfgFileName))
    //        {
    //            return null;
    //        }

    //        try
    //        {
    //            return cr.Load(File.OpenText(cfgFileName));
    //        }
    //        catch (Exception ex)
    //        {
    //            ExceptionDialog.showException(
    //                ex,
    //                "Configuration Load Error",
    //                string.Format("Failed to read configuration from file '{0}'.", cfgFileName)
    //            );
    //        }
    //        return null;
    //    }

    }
}
