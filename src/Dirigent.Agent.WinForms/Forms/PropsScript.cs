using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using WinFormsSyntaxHighlighter;

namespace Dirigent.Gui.WinForms
{
	public partial class frmScriptProperties : Form
	{
		ScriptDef _scriptDef;

		public frmScriptProperties( ScriptDef scriptDef )
		{
			InitializeComponent();

			_scriptDef = scriptDef;
			Text = $"[{scriptDef.Id}] Properties";


			string jsonString = JsonSerializer.Serialize( scriptDef, new JsonSerializerOptions() { WriteIndented=true, IncludeFields=true } );
			rtbAppProps.Text = jsonString;

            var syntaxHighlighter = new SyntaxHighlighter(rtbAppProps);

			JsonPatternsPreset.ApplyTo( syntaxHighlighter );

			syntaxHighlighter.ReHighlight();

		}

		void SetXmlString( string xmlText )
		{
			rtbAppProps.Clear();

			XmlReaderSettings settings = new XmlReaderSettings();

			var stream = new MemoryStream(Encoding.UTF8.GetBytes(xmlText ?? ""));
			int indent = 0;
			using (XmlReader reader = XmlReader.Create( stream, settings))
			{
				while(reader.Read())
				{
					switch (reader.NodeType)
					{
						case XmlNodeType.Element:
							rtbAppProps.SelectionColor = Color.Blue;
							rtbAppProps.AppendText("<");
							rtbAppProps.SelectionColor = Color.Brown;
							rtbAppProps.AppendText(reader.Name);
							rtbAppProps.SelectionColor = Color.Blue;
							rtbAppProps.AppendText(">");
							indent++;
	                        break;

						case XmlNodeType.Text:
							rtbAppProps.SelectionColor = Color.Blue;
							rtbAppProps.AppendText(reader.Value);
							break;

						case XmlNodeType.EndElement:
							indent--;
							rtbAppProps.SelectionColor = Color.Blue;
							rtbAppProps.AppendText("</");
							rtbAppProps.SelectionColor = Color.Brown;
							rtbAppProps.AppendText(reader.Name);
							rtbAppProps.SelectionColor = Color.Blue;
							rtbAppProps.AppendText(">");
							rtbAppProps.AppendText("\n");
							break;

						case XmlNodeType.Attribute:
							rtbAppProps.SelectionColor = Color.Brown;
							rtbAppProps.AppendText(reader.Name);
							rtbAppProps.AppendText(" = ");
							rtbAppProps.AppendText(reader.Value);
							rtbAppProps.AppendText("\n");
	                        break;

						default:
							Console.WriteLine("Other node {0} with value {1}",
											reader.NodeType, reader.Value);
							break;
					}
				}
			}
		}
	}
}
