using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Dirigent
{
	/// <summary>
	/// Resolves &lt;MachineTemplate&gt; elements: what machines of a site have in common - the file
	/// shares, the machine-wide log and configuration nodes - written once and used by
	/// <c>&lt;Machine Template="m.base;m.extra"&gt;</c>.
	/// </summary>
	/// <remarks>
	/// Two steps around <see cref="NodeTemplates.Apply"/>, because a node a template brings in may use a
	/// NodeTemplate, and may get its Id from it - which the replacing below goes by:
	/// <list type="number">
	/// <item>Apply copies the templates' attributes (the machine's own win) and children into each
	/// machine, so that NodeTemplates can expand the nodes there.</item>
	/// <item>Merge then re-composes each machine's children from the layers they came in: every template
	/// of the chain, the mixins left to right, the machine itself. A later layer's Share with the same
	/// Name, and its node with the same Id, replace the earlier one in its place.</item>
	/// </list>
	/// A config with no Template on a Machine is not touched at all, so what such a machine declares is
	/// read exactly as before.
	/// </remarks>
	public static class MachineTemplates
	{
		const string TemplateElement = "MachineTemplate";
		const string TemplateAttr = "Template";

		static readonly HashSet<string> NodeElementNames = new() { "File", "Folder", "VFolder", "FilePackage", "FileRef" };

		/// <summary>The children of one machine in the layers they came in, oldest first; the machine's own last.</summary>
		public sealed class Plan
		{
			readonly List<(XElement Machine, List<List<XElement>> Layers)> _machines = new();

			internal void Add( XElement machine, List<List<XElement>> layers ) => _machines.Add( ( machine, layers ) );

			/// <summary>Puts the children of every templated machine together, replacing as the layers say.</summary>
			public void Merge()
			{
				foreach( var (machine, layers) in _machines )
				{
					var merged = new List<XElement>();

					foreach( var layer in layers )
					{
						var earlier = merged.Count; // only what came before this layer can be replaced by it
						foreach( var child in layer )
							Put( merged, earlier, child );
					}

					machine.Elements().Remove();
					machine.Add( merged.Cast<object>().ToArray() );
				}
			}

			static void Put( List<XElement> merged, int earlier, XElement child )
			{
				var at = -1;

				if( child.Name.LocalName == "Share" )
				{
					var name = child.Attribute( "Name" )?.Value;
					if( !string.IsNullOrEmpty( name ) )
						at = merged.Take( earlier ).ToList().FindIndex( e => e.Name.LocalName == "Share" && e.Attribute( "Name" )?.Value == name );
				}
				else
				if( NodeElementNames.Contains( child.Name.LocalName ) )
				{
					var id = child.Attribute( "Id" )?.Value;
					if( !string.IsNullOrEmpty( id ) )
					{
						var same = Enumerable.Range( 0, earlier )
								.Where( i => NodeElementNames.Contains( merged[i].Name.LocalName ) && merged[i].Attribute( "Id" )?.Value == id )
								.ToList();

						if( same.Count == 1 )
						{
							at = same[0];
						}
						else
						if( same.Count > 1 )
						{
							// several inherited nodes share the Id: only the one with the same title is meant
							var title = child.Attribute( "Title" )?.Value;
							var equal = same.Where( i => merged[i].Attribute( "Title" )?.Value == title ).ToList();
							if( equal.Count > 0 ) at = equal[0];
						}
					}
				}

				if( at >= 0 )
					merged[at] = child;
				else
					merged.Add( child );
			}
		}

		sealed class Resolved
		{
			public readonly List<XAttribute> Attributes = new();
			public readonly List<List<XElement>> Layers = new();
		}

		/// <summary>
		/// First step: attributes and children of the templates are copied into each machine that names
		/// any. Returns what <see cref="Plan.Merge"/> needs once the NodeTemplates are expanded.
		/// </summary>
		/// <exception cref="ConfigurationErrorException">
		/// unknown template, cycle (in a chain or through mixins), a template without a Name, a duplicate Name,
		/// an IP or a Guid on a template
		/// </exception>
		public static Plan Apply( XElement root )
		{
			var templates = new Dictionary<string, XElement>();
			foreach( var t in root.Elements( TemplateElement ) )
			{
				var name = t.Attribute( "Name" )?.Value;
				if( string.IsNullOrEmpty( name ) )
					throw new ConfigurationErrorException( $"{TemplateElement} without a Name: {t}" );

				foreach( var forbidden in new[] { "an IP", "a Guid" } )
				{
					if( t.Attribute( forbidden.Split( ' ' )[1] ) is not null )
						throw new ConfigurationErrorException(
							$"{TemplateElement} '{name}' must not have {forbidden}: every machine made from it would get the same one" );
				}

				if( !templates.TryAdd( name, t ) )
					throw new ConfigurationErrorException( $"Duplicate {TemplateElement} '{name}'" );
			}

			var plan = new Plan();

			foreach( var machine in root.Elements( "Machine" ).Where( m => m.Attribute( TemplateAttr ) is not null ).ToList() )
			{
				var names = ParseNames( machine.Attribute( TemplateAttr )!.Value );
				machine.Attribute( TemplateAttr )!.Remove();

				var resolved = new Resolved();
				foreach( var name in names )
					Add( resolved, ResolveOne( name, templates, new List<string>() ) );

				foreach( var a in resolved.Attributes )
				{
					if( machine.Attribute( a.Name ) is null )
						machine.SetAttributeValue( a.Name, a.Value );
				}

				// the machine's own children are the last layer, and stay after the template's
				var own = machine.Elements().ToList();
				var layers = resolved.Layers.Select( l => l.Select( c => new XElement( c ) ).ToList() ).ToList();
				layers.Add( own );

				machine.Elements().Remove();
				machine.Add( layers.SelectMany( l => l ).Cast<object>().ToArray() );

				plan.Add( machine, layers );
			}

			return plan;
		}

		static List<string> ParseNames( string list )
			=> list.Split( ';', StringSplitOptions.RemoveEmptyEntries ).Select( n => n.Trim() ).Where( n => n.Length > 0 ).ToList();

		/// <summary>A later one wins: its attributes replace the earlier ones', its layers come after.</summary>
		static void Add( Resolved into, Resolved later )
		{
			foreach( var a in later.Attributes )
			{
				into.Attributes.RemoveAll( x => x.Name == a.Name );
				into.Attributes.Add( a );
			}
			into.Layers.AddRange( later.Layers );
		}

		static Resolved ResolveOne( string name, Dictionary<string, XElement> templates, List<string> chain )
		{
			var at = chain.IndexOf( name );
			if( at >= 0 )
				throw new ConfigurationErrorException( $"{TemplateElement} cycle: {string.Join( " -> ", chain.Skip( at ).Append( name ) )}" );

			if( !templates.TryGetValue( name, out var t ) )
				throw new ConfigurationErrorException( $"{TemplateElement} '{name}' not found" );

			var res = new Resolved();
			var next = new List<string>( chain ) { name };

			// the parents first, then this template over them
			var parents = t.Attribute( TemplateAttr )?.Value;
			if( !string.IsNullOrEmpty( parents ) )
			{
				foreach( var parent in ParseNames( parents ) )
					Add( res, ResolveOne( parent, templates, next ) );
			}

			foreach( var a in t.Attributes() )
			{
				if( a.Name == "Name" || a.Name == TemplateAttr )
					continue;
				res.Attributes.RemoveAll( x => x.Name == a.Name );
				res.Attributes.Add( a );
			}

			res.Layers.Add( t.Elements().ToList() );
			return res;
		}
	}
}
