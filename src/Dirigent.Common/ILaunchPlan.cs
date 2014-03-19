using System;
namespace Dirigent.Common
{
    public interface ILaunchPlan : IEquatable<ILaunchPlan>
    {
        System.Collections.Generic.IEnumerable<Dirigent.Common.AppDef> getAppDefs();
        string Name { get; }
    }
}
