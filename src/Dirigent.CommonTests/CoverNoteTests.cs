using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Scripts.BuiltIn;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent.Tests
{
	/// <summary>
	/// What `_comment.txt` says about the machines whose files never made it into the archive.
	/// </summary>
	/// <remarks>
	/// The reason this exists: a machine that could not deliver leaves no trace inside the archive -
	/// no folder, no `_incomplete.txt`, nothing - so an incomplete collection looked exactly like a
	/// complete one to whoever opened it later. The dialog that reported it is long gone by then.
	///
	/// Tested here rather than at tier 1 because a tier-1 bed cannot produce an unreachable machine:
	/// every machine of it answers on loopback, so each one either owns the download folder or shares
	/// an address with the machine that does, and the collection always finds a way in. It takes the
	/// two real machines of tier 3 to see it happen.
	/// </remarks>
	[TestClass()]
	public class CoverNoteTests
	{
		/// <summary>Stands in for the note's own machine description, which needs a live master.</summary>
		static string Describe( string machine ) => $"{machine} [192.168.0.1]";

		[TestMethod()]
		public void NothingIsSaidWhenEveryMachineDeliveredTest()
		{
			Assert.IsNull( DownloadZipped.DescribeMissing( new Dictionary<string, string>(), Describe ),
				"a complete archive must not carry a warning about nothing" );
		}

		[TestMethod()]
		public void AMachineThatCouldNotDeliverIsNamedWithItsReasonTest()
		{
			var unreachable = new Dictionary<string, string>()
			{
				{ "BackEnd", "No file share of FrontEnd covers its download folder, so BackEnd has no way of uploading the files there." },
			};

			var text = DownloadZipped.DescribeMissing( unreachable, Describe )!;

			StringAssert.Contains( text, "NOT COLLECTED",
				"the reader has to see it without looking for it" );
			StringAssert.Contains( text, "BackEnd [192.168.0.1]", "the machine is named, and located" );
			StringAssert.Contains( text, "no way of uploading",
				"and the reason travels with it - months later nobody remembers" );
		}

		[TestMethod()]
		public void SeveralMachinesComeInNameOrderTest()
		{
			// the order a dictionary yields is not one anybody can look something up in
			var unreachable = new Dictionary<string, string>()
			{
				{ "zulu", "no share" },
				{ "alpha", "no share" },
				{ "mike", "no share" },
			};

			var text = DownloadZipped.DescribeMissing( unreachable, Describe )!;

			var order = new[] { "alpha", "mike", "zulu" }
					.Select( m => text.IndexOf( m, StringComparison.Ordinal ) )
					.ToList();

			CollectionAssert.AreEqual( order.OrderBy( x => x ).ToList(), order,
				$"machines should be named in name order:\n{text}" );
			Assert.IsTrue( order.All( i => i >= 0 ), $"all of them should be named:\n{text}" );
		}
	}
}
