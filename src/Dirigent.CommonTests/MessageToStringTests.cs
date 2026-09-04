using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Dirigent.Tests
{
	/// <summary>
	/// Describing a message must never fail, whatever the message happens to carry.
	/// </summary>
	/// <remarks>
	/// This is not about tidy logs. `Message.ToString()` is called from inside the dispatch path -
	/// `Server.SendToAllSubscribed` logs the message *before* the loop that sends it - so a
	/// description that throws does not spoil a log line, it stops the message from being sent at
	/// all, and fails whatever operation was riding on it.
	///
	/// That happened in production: `string.Format( $"...{Title}..." )` parses the finished string a
	/// second time as a format template, so a brace arriving in any interpolated value was read as a
	/// placeholder. The trigger was a VFS node whose `Mask="{LocalConfig,GatewayConfig}.xml"` - a
	/// form `Glob.ParseMask` exists to support - and the symptom was every download failing at once
	/// with a FormatException naming nothing recognisable.
	///
	/// Braces are legitimate in masks, `%` in Windows paths, quotes in XML: all of them are
	/// format-significant somewhere, and any of them can end up in a title or an argument.
	/// </remarks>
	[TestClass()]
	public class MessageToStringTests
	{
		/// <summary>Everything that has ever been format-significant, in one string.</summary>
		const string Nasty = "{LocalConfig,GatewayConfig}.xml 100% \"quoted\" {0} {{braced}} \\path";

		[TestMethod()]
		public void AScriptTitleCarryingBracesDoesNotBreakItsMessageTest()
		{
			// the production case: the title of a resolve step is the node definition itself
			var msg = new Net.StartScriptMessage(
				"m1_gui_1",
				Guid.NewGuid(),
				"BuiltIns/ResolveVfsPath.cs",
				null,
				null,
				@"Resolve <Folder Id=""cfg"" Mask=""{LocalConfig,GatewayConfig}.xml"" />",
				"BackEnd" );

			var text = msg.ToString();

			StringAssert.Contains( text, "{LocalConfig,GatewayConfig}.xml",
				"the description carries what the message carries" );
			StringAssert.Contains( text, "BuiltIns/ResolveVfsPath.cs" );
			StringAssert.Contains( text, "BackEnd" );
		}

		[TestMethod()]
		public void APlanNameCarryingBracesDoesNotBreakItsMessageTest()
		{
			// the same shape, one message over - unreachable today, but only by luck of what it
			// interpolates
			var msg = new Net.SetAppEnabledMessage( "m1_gui_1", Nasty, new AppIdTuple( "m1", "a" ), true );

			var text = msg.ToString();

			StringAssert.Contains( text, "m1.a" );
		}

		[TestMethod()]
		public void NoMessageBreaksOnAnAwkwardStringTest()
		{
			// The systematic net: every message that can be built, with every string it holds set to
			// something format-significant. A message added later gets this for free.
			var messageTypes = typeof( Net.Message ).Assembly
					.GetTypes()
					.Where( t => typeof( Net.Message ).IsAssignableFrom( t ) && !t.IsAbstract )
					.OrderBy( t => t.Name )
					.ToList();

			Assert.IsTrue( messageTypes.Count > 30,
				$"expected the whole message set, found {messageTypes.Count}" );

			var covered = new List<string>();
			var failures = new List<string>();

			foreach( var type in messageTypes )
			{
				if( type.GetConstructor( Type.EmptyTypes ) is null )
					continue; // nothing to build it from here; the serializer needs one, so this is rare

				object msg;
				try { msg = Activator.CreateInstance( type )!; }
				catch { continue; }

				FillStrings( msg );

				try
				{
					var text = msg.ToString();
					covered.Add( type.Name );

					if( text is null || !text.Contains( type.Name.Replace( "Message", "" ),
														StringComparison.OrdinalIgnoreCase ) )
					{
						// not a failure - a description need not name its type - but it must exist
						Assert.IsNotNull( text, $"{type.Name}.ToString() returned null" );
					}
				}
				catch( Exception e )
				{
					failures.Add( $"{type.Name}: {e.GetType().Name}: {Tools.JustFirstLine( e.Message )}" );
				}
			}

			Assert.AreEqual( 0, failures.Count,
				"a message that cannot describe itself cannot be sent:\n" + string.Join( "\n", failures ) );

			Assert.IsTrue( covered.Count > 30,
				$"only {covered.Count} messages were actually exercised, which is too few to trust" );
		}

		[TestMethod()]
		public void ADescriptionThatFailsCostsALogLineNotTheMessageTest()
		{
			// Defence in depth for the next one of these: the dispatch path describes a message
			// through Tools.SafeToString, so a ToString() that throws produces a puzzling log line
			// rather than an unsent message and a failed operation.
			var text = Tools.SafeToString( new Unspeakable() );

			StringAssert.Contains( text, nameof( Unspeakable ), $"it should say what could not speak: {text}" );
			StringAssert.Contains( text, "no idea", $"and why: {text}" );

			Assert.AreEqual( "(null)", Tools.SafeToString( null ) );
			Assert.AreEqual( "plain", Tools.SafeToString( "plain" ) );
		}

		class Unspeakable
		{
			public override string ToString() => throw new Exception( "no idea what I am" );
		}

		/// <summary>Puts an awkward value into every string field and property of an object.</summary>
		static void FillStrings( object target )
		{
			foreach( var field in target.GetType().GetFields( BindingFlags.Public | BindingFlags.Instance ) )
			{
				if( field.FieldType == typeof( string ) && !field.IsInitOnly )
					field.SetValue( target, Nasty );
			}

			foreach( var prop in target.GetType().GetProperties( BindingFlags.Public | BindingFlags.Instance ) )
			{
				if( prop.PropertyType == typeof( string ) && prop.CanWrite )
					prop.SetValue( target, Nasty );
			}
		}
	}
}
