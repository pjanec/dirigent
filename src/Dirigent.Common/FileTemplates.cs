using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Dirigent
{
	/// <summary>
	/// Resolves &lt;FileTemplate&gt; elements: defaults for file nodes (File, Folder, VFolder,
	/// FilePackage, FileRef), so that attributes repeated on dozens of nodes can be written once.
	/// </summary>
	/// <remarks>
	/// Done as one pass over the XML before anything is read from it, so that the rest of the reader -
	/// and the raw-XML reads of Mask/MaxFiles/MaxSeconds in the file registry - see plain nodes and
	/// need no idea that templates exist.
	///
	/// Merge rule, the same as for AppTemplate: the template's attributes are defaults, the node's own
	/// attributes win. The template's child elements (actions, nested nodes) are copied in front of the
	/// node's own children, so a node's action with the same Id replaces the template's one (the reader
	/// replaces actions by Id in document order). A template may name a base template itself: the
	/// derived template's attributes win, children are concatenated base first.
	///
	/// A config that has no FileTemplate and no Template= on a file node is not touched at all.
	/// </remarks>
	public static class FileTemplates
	{
		const string TemplateElement = "FileTemplate";
		const string TemplateAttr = "Template";

		static readonly HashSet<string> NodeElementNames = new() { "File", "Folder", "VFolder", "FilePackage", "FileRef" };

		sealed class Resolved
		{
			public readonly List<XAttribute> Attributes = new();
			public readonly List<XElement> Children = new();
		}

		/// <summary>
		/// Expands the templates in the document below <paramref name="root"/>, in place.
		/// </summary>
		/// <exception cref="ConfigurationErrorException">
		/// unknown template, template cycle, template without Name, duplicate Name, Guid on a template
		/// </exception>
		public static void Apply( XElement root )
		{
			// the element used to be called NodeTemplate; a config still using the name would silently lose its defaults
			if( root.Elements( "NodeTemplate" ).Any() )
				throw new ConfigurationErrorException( "<NodeTemplate> was renamed to <FileTemplate>" );

			var templates = new Dictionary<string, XElement>();
			foreach( var t in root.Elements( TemplateElement ) )
			{
				var name = t.Attribute( "Name" )?.Value;
				if( string.IsNullOrEmpty( name ) )
					throw new ConfigurationErrorException( $"{TemplateElement} without a Name: {t}" );
				if( t.Attribute( "Guid" ) is not null )
					throw new ConfigurationErrorException(
						$"{TemplateElement} '{name}' must not have a Guid: every node made from it would get the same one" );
				if( !templates.TryAdd( name, t ) )
					throw new ConfigurationErrorException( $"Duplicate {TemplateElement} '{name}'" );
			}

			// a snapshot; the nodes copied in from a template are handled where they are inserted
			var users = root.Descendants()
				.Where( e => IsNode( e ) && e.Attribute( TemplateAttr ) is not null
							 && !e.Ancestors( TemplateElement ).Any() )
				.ToList();

			foreach( var e in users )
				ApplyTo( e, templates, new List<string>() );
		}

		static bool IsNode( XElement e ) => NodeElementNames.Contains( e.Name.LocalName );

		static void ApplyTo( XElement e, Dictionary<string, XElement> templates, List<string> chain )
		{
			var attr = e.Attribute( TemplateAttr );
			if( attr is null )
				return;

			var name = attr.Value;
			CheckNotInChain( name, chain );

			var resolved = Resolve( name, templates, new List<string>() );

			attr.Remove();

			foreach( var a in resolved.Attributes )
			{
				if( e.Attribute( a.Name ) is null )
					e.SetAttributeValue( a.Name, a.Value );
			}

			// own children stay after the template's, so that they win
			var inserted = resolved.Children.Select( c => new XElement( c ) ).ToList();
			e.AddFirst( inserted.Cast<object>().ToArray() );

			// the copied-in nodes may use templates themselves, at any depth (a package template
			// holding a folder that holds a templated file)
			var newChain = new List<string>( chain ) { name };
			foreach( var node in inserted.SelectMany( c => c.DescendantsAndSelf() ).Where( IsNode ).ToList() )
				ApplyTo( node, templates, newChain );
		}

		static void CheckNotInChain( string name, List<string> chain )
		{
			var at = chain.IndexOf( name );
			if( at >= 0 )
			{
				var cycle = chain.Skip( at ).Append( name );
				throw new ConfigurationErrorException( $"{TemplateElement} cycle: {string.Join( " -> ", cycle )}" );
			}
		}

		static Resolved Resolve( string name, Dictionary<string, XElement> templates, List<string> chain )
		{
			CheckNotInChain( name, chain );

			if( !templates.TryGetValue( name, out var t ) )
				throw new ConfigurationErrorException( $"{TemplateElement} '{name}' not found" );

			var res = new Resolved();
			var baseName = t.Attribute( TemplateAttr )?.Value;
			if( !string.IsNullOrEmpty( baseName ) )
			{
				var b = Resolve( baseName, templates, new List<string>( chain ) { name } );
				res.Attributes.AddRange( b.Attributes );
				res.Children.AddRange( b.Children );
			}

			foreach( var a in t.Attributes() )
			{
				if( a.Name == "Name" || a.Name == TemplateAttr )
					continue;
				res.Attributes.RemoveAll( x => x.Name == a.Name );
				res.Attributes.Add( a );
			}

			res.Children.AddRange( t.Elements() );
			return res;
		}
	}
}
