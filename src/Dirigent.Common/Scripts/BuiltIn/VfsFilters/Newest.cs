using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Dirigent;

namespace Dirigent.Scripts.BuiltIn.VfsFilter
{

// This script is called to get the actual full file name when resolving
// the <File/> VFS node with filter = "Newest" and Path="c:\some\local\path"
// The resolving code runs this script on the machine where the file resides
// to get the name of the file. 
// Returns the full path to the newest file in given folder, matching given mask.
// Args = FileDef
//   .MachineId = machine where the Path resides
//   .Path = folder
// 	 .Mask = file mask (glob style)
// Result = plain VfsNodeDef
//   .MachineId = machine where the Path resides
//   .Path = full local file path

public class Newest : Script
{
	private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

	public static readonly string _Name = "BuiltIn.VfsFilter.Newest";
}

}
