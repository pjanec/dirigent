using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//https://stackoverflow.com/questions/54877077/equality-and-polymorphism
namespace System {
  public static partial class ExtensionMethods {
    public static bool Equals<T>(this T inst, object? obj, Func<T, bool> thisEquals) where T : IEquatable<T> =>
      object.ReferenceEquals(inst, obj) // same reference ->  equal
      || !(obj is null) // this is not null but obj is -> not equal
      && obj.GetType() == inst.GetType() // obj is more derived than this -> not equal
      && obj is T o // obj cannot be cast to this type -> not equal
      && thisEquals(o);
  }
}
