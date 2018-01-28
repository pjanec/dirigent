using System;
namespace Dirigent.Common
{
    public interface ILaunchPlan : IEquatable<ILaunchPlan>
    {
        System.Collections.Generic.IEnumerable<Dirigent.Common.AppDef> getAppDefs();
        string Name { get; }
		
		/// <summary>
		/// Number of second since plan start till all apps shall be already running
		/// (plas state will be se to Failure if it takes longer)
		/// If negative, no timeout will be checked.
		/// </summary>
		double StartTimeout { get; }
    }
}
