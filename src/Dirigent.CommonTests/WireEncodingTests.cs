using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Net;
using MessagePack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Dirigent.Tests
{
	/// <summary>
	/// The master protocol encodes every class with integer keys (no field names) and LZ4. These tests keep
	/// that right: a member added to a wire class without a key fails here instead of silently vanishing
	/// from the wire, and every message type survives the real codec.
	/// </summary>
	[TestClass()]
	public class WireEncodingTests
	{
		/// <summary>The types a Message can be on the wire.</summary>
		static IEnumerable<Type> UnionTypes() =>
			typeof( Message ).GetCustomAttributes<UnionAttribute>().Select( a => a.SubType );

		/// <summary>
		/// Everything reachable from the union: the message types, their bases, the types of their members
		/// (through collections, dictionaries, nullables and arrays) and the subtypes of any union met on the way.
		/// </summary>
		static HashSet<Type> WireTypes()
		{
			var seen = new HashSet<Type>();
			var todo = new Queue<Type>();
			todo.Enqueue( typeof( Message ) );
			foreach( var t in UnionTypes() ) todo.Enqueue( t );

			void Add( Type? t )
			{
				if( t is null ) return;
				if( t.IsArray ) { Add( t.GetElementType() ); return; }
				if( t.IsGenericType )
				{
					foreach( var a in t.GetGenericArguments() ) Add( a );
					if( !t.Assembly.GetName().Name!.StartsWith( "Dirigent" ) ) return;
				}
				var under = Nullable.GetUnderlyingType( t );
				if( under != null ) { Add( under ); return; }
				if( t.IsEnum || t.IsPrimitive || t.Assembly != typeof( Message ).Assembly ) return;
				if( seen.Add( t ) ) todo.Enqueue( t );
			}

			while( todo.Count > 0 )
			{
				var t = todo.Dequeue();
				seen.Add( t );
				Add( t.BaseType );
				foreach( var u in t.GetCustomAttributes<UnionAttribute>( false ) ) Add( u.SubType );
				foreach( var m in SerializedCandidates( t ) ) Add( MemberType( m ) );
			}
			return seen;
		}

		static Type MemberType( MemberInfo m ) => m is FieldInfo f ? f.FieldType : ( (PropertyInfo) m ).PropertyType;

		/// <summary>Public instance fields and properties with a setter, declared by the type itself.</summary>
		static IEnumerable<MemberInfo> SerializedCandidates( Type t )
		{
			var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
			foreach( var f in t.GetFields( flags ) ) yield return f;
			foreach( var p in t.GetProperties( flags ) )
				if( p.SetMethod != null && p.SetMethod.IsPublic && p.GetIndexParameters().Length == 0 ) yield return p;
		}

		[TestMethod]
		public void EveryWireClassIsAMessagePackObject()
		{
			var bad = WireTypes()
				.Where( t => !t.IsEnum && !t.IsInterface )
				.Where( t => t.GetCustomAttribute<MessagePackObjectAttribute>( false ) is null )
				.Select( t => t.FullName ).ToList();
			Assert.AreEqual( 0, bad.Count, "wire classes without [MessagePackObject]: " + string.Join( ", ", bad ) );
		}

		[TestMethod]
		public void EveryWireMemberHasAKeyOrIsIgnored()
		{
			var problems = new List<string>();
			foreach( var t in WireTypes().Where( t => !t.IsEnum ) )
			{
				foreach( var m in SerializedCandidates( t ) )
				{
					bool key = m.GetCustomAttribute<KeyAttribute>() != null;
					bool ign = m.GetCustomAttribute<IgnoreMemberAttribute>() != null;
					if( !key && !ign ) problems.Add( $"{t.FullName}.{m.Name}: no [Key] and no [IgnoreMember]" );
				}
			}
			Assert.AreEqual( 0, problems.Count, string.Join( "\n", problems ) );
		}

		[TestMethod]
		public void NoTwoMembersOfAHierarchyShareAKey()
		{
			var problems = new List<string>();
			foreach( var t in WireTypes().Where( t => !t.IsEnum && !t.IsInterface ) )
			{
				// base and derived share one key space
				var keys = new Dictionary<int, string>();
				for( var c = t; c != null && c != typeof( object ); c = c.BaseType )
				{
					foreach( var m in c.GetMembers( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly ) )
					{
						var k = m.GetCustomAttribute<KeyAttribute>();
						if( k?.IntKey is null ) continue;
						var name = $"{c.Name}.{m.Name}";
						if( keys.TryGetValue( k.IntKey.Value, out var other ) )
							problems.Add( $"{t.FullName}: key {k.IntKey} used by {other} and {name}" );
						else
							keys[k.IntKey.Value] = name;
					}
				}
			}
			Assert.AreEqual( 0, problems.Count, string.Join( "\n", problems ) );
		}

		[TestMethod]
		public void KeysAreIntegersNotNames()
		{
			var bad = new List<string>();
			foreach( var t in WireTypes().Where( t => !t.IsEnum ) )
				foreach( var m in t.GetMembers( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly ) )
					if( m.GetCustomAttribute<KeyAttribute>() is { IntKey: null } ) bad.Add( $"{t.FullName}.{m.Name}" );
			Assert.AreEqual( 0, bad.Count, string.Join( ", ", bad ) );
		}

		[TestMethod]
		public void TheWireIsNotContractless()
		{
			var ident = new ClientIdent() { Sender = "UniqueSenderValue", SubscribedTo = EMsgRecipCateg.Gui };
			var bytes = MessagePackSerializer.Serialize<Message>( ident, MsgPackCodec.Options );
			var text = Encoding.UTF8.GetString( bytes );
			Assert.IsFalse( text.Contains( "SubscribedTo" ), "a field name travels on the wire: the encoding is contractless again" );
			Assert.IsFalse( text.Contains( "Sender\0" ) || text.Contains( "¦Sender" ), "a field name travels on the wire" );
			Assert.AreEqual( MessagePackType.Array, new MessagePackReader( bytes ).NextMessagePackType, "the payload is a map, not an array" );
			// the contractless resolver honours [Key] where there is one, so the payload above proves little; what it does
			// differently is to write a class nobody marked, field names and all. Here that is an error.
			Assert.ThrowsException<MessagePackSerializationException>( () => MessagePackSerializer.Serialize( new UnmarkedClass { Value = 1 }, MsgPackCodec.Options ),
				"a class without [MessagePackObject] was written to the wire anyway: the encoding is contractless again" );
			Assert.AreEqual( MessagePackCompression.Lz4BlockArray, MsgPackCodec.Options.Compression );
			Assert.AreSame( MsgPackCodec.Options, MessagePackSerializer.DefaultOptions );
		}

		public class UnmarkedClass { public int Value; }

		// ---------------------------------------------------------------- round trip

		[TestMethod]
		public void EveryMessageTypeSurvivesTheCodec()
		{
			var failures = new List<string>();
			foreach( var t in UnionTypes() )
			{
				try
				{
					var filler = new Filler();
					var msg = (Message) filler.Make( t, 0 )!;
					var wire = new System.IO.MemoryStream();
					MsgPackCodec.Serialize<Message>( wire, msg );

					Message? received = null;
					var codec = new MsgPackCodec() { MessageReceived = m => received = m };
					codec.ReceivedMessagePart( wire.GetBuffer(), 0, wire.Length );
					Assert.IsNotNull( received, "nothing came out of the codec" );
					Assert.AreEqual( t, received!.GetType() );

					var a = MessagePackSerializer.Serialize<Message>( msg, MsgPackCodec.Options );
					var b = MessagePackSerializer.Serialize<Message>( received, MsgPackCodec.Options );
					if( !a.SequenceEqual( b ) ) failures.Add( $"{t.Name}: the received message differs from the sent one: {BitConverter.ToString( a )} vs {BitConverter.ToString( b )}" );
				}
				catch( Exception ex )
				{
					failures.Add( $"{t.Name}: {ex.GetType().Name} {ex.Message}" );
				}
			}
			Assert.AreEqual( 0, failures.Count, string.Join( "\n", failures ) );
		}

		[TestMethod]
		public void TheFillerMakesEveryKeyedMemberNonDefault()
		{
			// the round trip is only as good as the data: the sample of the biggest message has a value in each keyed member
			var msg = (AppsStateMessage) new Filler().Make( typeof( AppsStateMessage ), 0 )!;
			Assert.IsTrue( msg.AppsState!.Count > 0 );
			var state = msg.AppsState.Values.First();
			Assert.AreNotEqual( default( DateTime ), state.LastChange );
		}

		[TestMethod]
		public void AnAppDefClonesThroughTheCodec()
		{
			var def = (AppDef) new Filler().Make( typeof( AppDef ), 0 )!;
			var clone = def.Clone();
			Assert.IsTrue( MessagePackSerializer.Serialize( def, MsgPackCodec.Options ).SequenceEqual( MessagePackSerializer.Serialize( clone, MsgPackCodec.Options ) ) );
		}

		/// <summary>Builds an instance of any wire type with non-default values in every settable member.</summary>
		class Filler
		{
			int _n;
			public int Filled;

			public object? Make( Type t, int depth )
			{
				if( depth > 4 ) return null;
				var under = Nullable.GetUnderlyingType( t );
				if( under != null ) t = under;

				if( t == typeof( string ) ) return "s" + ( ++_n );
				if( t == typeof( bool ) ) return true;
				if( t == typeof( char ) ) return 'x';
				if( t == typeof( DateTime ) ) return new DateTime( 2026, 11, 23, 10, 20, 30, DateTimeKind.Utc ).AddSeconds( ++_n );
				if( t == typeof( TimeSpan ) ) return TimeSpan.FromSeconds( ++_n );
				if( t == typeof( Guid ) ) return Guid.NewGuid();
				if( t.IsEnum )
				{
					var vals = Enum.GetValues( t ).Cast<object>().Where( v => Convert.ToInt64( v ) != 0 ).ToList();
					return vals.Count > 0 ? vals[0] : Activator.CreateInstance( t );
				}
				if( t.IsPrimitive )
				{
					++_n;
					if( t == typeof( float ) ) return _n + 0.5f;
					if( t == typeof( double ) ) return _n + 0.5;
					return Convert.ChangeType( _n + 1, t );
				}
				if( t == typeof( byte[] ) ) return new byte[] { 1, 2, 3, (byte) _n };
				if( t.IsArray )
				{
					var arr = Array.CreateInstance( t.GetElementType()!, 1 );
					arr.SetValue( Make( t.GetElementType()!, depth + 1 ), 0 );
					return arr;
				}
				if( t.IsGenericType )
				{
					var def = t.GetGenericTypeDefinition();
					var args = t.GetGenericArguments();
					if( def == typeof( Dictionary<,> ) || def == typeof( IDictionary<,> ) )
					{
						var d = (IDictionary) Activator.CreateInstance( typeof( Dictionary<,> ).MakeGenericType( args ) )!;
						for( int i = 0; i < 2; i++ )
						{
							var k = Make( args[0], depth + 1 ); var v = Make( args[1], depth + 1 );
							if( k != null ) d[k] = v;
						}
						return d;
					}
					if( def == typeof( List<> ) || def == typeof( IList<> ) || def == typeof( IEnumerable<> ) || def == typeof( ICollection<> ) )
					{
						var l = (IList) Activator.CreateInstance( typeof( List<> ).MakeGenericType( args ) )!;
						var e = Make( args[0], depth + 1 );
						if( e != null ) l.Add( e );
						return l;
					}
					if( def == typeof( HashSet<> ) )
					{
						var h = Activator.CreateInstance( t )!;
						var e = Make( args[0], depth + 1 );
						if( e != null ) t.GetMethod( "Add" )!.Invoke( h, new[] { e } );
						return h;
					}
				}

				// a class or struct of ours
				if( t.IsAbstract || t.IsInterface )
				{
					var sub = t.GetCustomAttributes<UnionAttribute>( false ).Select( u => u.SubType ).FirstOrDefault( s => !s.IsAbstract );
					if( sub is null ) return null;
					t = sub;
				}
				object? o;
				try { o = Activator.CreateInstance( t, true ); } catch { return null; }
				if( o is null ) return null;
				Filled++;
				for( var c = t; c != null && c != typeof( object ); c = c.BaseType )
				{
					foreach( var m in SerializedCandidates( c ) )
					{
						if( m.GetCustomAttribute<IgnoreMemberAttribute>() != null ) continue;
						var v = Make( MemberType( m ), depth + 1 );
						if( v is null ) continue;
						if( m is FieldInfo f ) { if( !f.IsInitOnly ) f.SetValue( o, v ); }
						else ( (PropertyInfo) m ).SetValue( o, v );
					}
				}
				return o;
			}
		}
	}
}
