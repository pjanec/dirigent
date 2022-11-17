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
	public partial class frmAppProperties : Form
	{
		AppDef _appDef;

		public frmAppProperties( AppDef appDef )
		{
			InitializeComponent();

			_appDef = appDef;
			Text = $"[{appDef.Id}] Properties";


			string jsonString = JsonSerializer.Serialize( appDef, new JsonSerializerOptions() { WriteIndented=true, IncludeFields=true } );
			rtbAppProps.Text = jsonString;

            var syntaxHighlighter = new SyntaxHighlighter(rtbAppProps);

            //// multi-line comments
            //syntaxHighlighter.AddPattern(new PatternDefinition(new Regex(@"/\*(.|[\r\n])*?\*/", RegexOptions.Multiline | RegexOptions.Compiled)), new SyntaxStyle(Color.DarkSeaGreen, false, true));
            //// singlie-line comments
            //syntaxHighlighter.AddPattern(new PatternDefinition(new Regex(@"//.*?$", RegexOptions.Multiline | RegexOptions.Compiled)), new SyntaxStyle(Color.Green, false, true));
            // fields
            syntaxHighlighter.AddPattern(new PatternDefinition(@"\""([^""]|\""\"")*\"":"), new SyntaxStyle(Color.Brown));
            // double quote strings
            syntaxHighlighter.AddPattern(new PatternDefinition(@"\""([^""]|\""\"")*\"""), new SyntaxStyle(Color.Red));
            // single quote strings
            syntaxHighlighter.AddPattern(new PatternDefinition(@"\'([^']|\'\')*\'"), new SyntaxStyle(Color.Salmon));
            // operators
            syntaxHighlighter.AddPattern(new PatternDefinition("=", "[", "]", "{", "}"), new SyntaxStyle(Color.Black));
            // numbers
            syntaxHighlighter.AddPattern(new PatternDefinition(@"\d+\.\d+|\d+"), new SyntaxStyle(Color.Black));
            // keywords1
            syntaxHighlighter.AddPattern(new PatternDefinition("null", "false", "true"), new SyntaxStyle(Color.Blue));
            //// keywords2
            //syntaxHighlighter.AddPattern(new CaseInsensitivePatternDefinition("public", "partial", "class", "void"), new SyntaxStyle(Color.Navy, true, false));

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
