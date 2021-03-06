﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Dirigent
{
	[ProtoBuf.ProtoContract]
	public class PlanScriptDef
	{
		/// <summary>
		/// Path to the script file; Either absolute or relative to the SharedConfig file location
		/// </summary>
		[ProtoBuf.ProtoMember( 1 )]
		public string Name = string.Empty;
	}

	[ProtoBuf.ProtoContract]
	public class PlanDef
	{
		[ProtoBuf.ProtoMember( 1 )]
		public string Name = string.Empty;

		[ProtoBuf.ProtoMember( 2 )]
		public List<AppDef> AppDefs = new List<AppDef>();

		[ProtoBuf.ProtoMember( 3 )]
		public PlanScriptDef? PlanScriptDef;

		[ProtoBuf.ProtoMember( 4 )]
		public double StartTimeout = 0.0;

		// semicolon separated list of "paths" like "main/examples;"GUI might use this for showing items in a folder tree
		[ProtoBuf.ProtoMember( 5 )]
		public string Groups = string.Empty;

		public bool Equals( PlanDef other )
		{
			if( other == null )
				return false;

			if( this.Name == other.Name
					&&
					this.AppDefs.SequenceEqual( other.AppDefs )
			  )
				return true;
			else
				return false;
		}

		public override bool Equals( Object? obj )
		{
			if( obj == null )
				return false;

			var typed = obj as PlanDef;
			if( typed == null )
				return false;
			else
				return Equals( typed );
		}

		public override int GetHashCode()
		{
			return this.Name.GetHashCode();
		}
	}

	public class SharedConfig
	{
		public List<AppDef> AppDefaults = new List<AppDef>();
		public List<PlanDef> Plans = new List<PlanDef>();
		public List<ScriptDef> Scripts = new List<ScriptDef>();

	}

	public class LocalConfig
	{
		/// <summary>
		/// The XML document with local configuration
		/// </summary>
		public System.Xml.Linq.XDocument xmlDoc;
		public List<System.Xml.Linq.XElement> folderWatcherXmls = new List<System.Xml.Linq.XElement>();

		public LocalConfig( System.Xml.Linq.XDocument xmlDoc )
		{
			this.xmlDoc = xmlDoc;
		}
	}
}
