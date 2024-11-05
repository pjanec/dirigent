using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dirigent
{
    public class CommandRepository
    {
        Master ctrl;
        public delegate ICommand CmdCreatorDeleg(Master ctrl, string requestorId);
        Dictionary<string, CmdCreatorDeleg> commands = new Dictionary<string, CmdCreatorDeleg>();

        public CommandRepository( Master ctrl )
        {
            this.ctrl = ctrl;
        }

        public void Register( string name, CmdCreatorDeleg creator )
        {
            commands[name] = creator;
        }

        public ICommand? Create( string name, string requestorId )
        {
            if (commands.ContainsKey(name))
            {
                return commands[name](ctrl, requestorId);
            }

            return null;
        }

        ICommand ParseSingleCommand( string requestorId, IList<string> cmdLineTokens )
        {
            string cmdName = cmdLineTokens[0];

            ICommand? cmd = Create(cmdName, requestorId);
            if (cmd == null)
            {
                throw new UnknownCommandException(cmdName);
            }

            cmd.Args = new List<string>(cmdLineTokens);
            cmd.Args.RemoveAt(0);
			return cmd;
        }

        List<List<string>> ParseSubcommands( IList<string> cmdLineTokens )
        {
            
            // strip the tokens containing semicolons into separate sequences of tokens ( = commands)
            List<string> newTokens = new List<string>();

            foreach( var token in cmdLineTokens )
            {
                if( token.IndexOf(";") < 0 ) // no semicolon in a token, simply add to the command 
                {
                    newTokens.Add( token );
                }
                else // semicolons in token, split
                {
                    // ;command1;command2;
                    var subtokens = token.Split( new char[] {';'} );
                
                    // all inner strings between semicolons
                    for(int i=0; i < subtokens.Length; i++)
                    {
                        var newToken = subtokens[i].Trim();
                        if( !string.IsNullOrEmpty( newToken ) )
                        {
                            newTokens.Add( newToken );
                        }
                        if( i < subtokens.Length-1 ) // do not add semicolons to the last one
                        {
                            newTokens.Add( ";" );
                        }
                    }
               }     
            }
            // split tokens into commands (a semicolon token is the separator)
                
            List<List<string>> commands = new List<List<string>>();

            List<string> command = new List<string>();
            foreach( var t in newTokens )
            {
                if( t == ";" )
                {
                    // end current command
                    if( command.Count > 0 )
                    {
                        commands.Add( command );
                        command = new List<string>();
                    }
                }
                else
                {
                    command.Add( t );
                }
            }
            
            // last command
            if( command.Count > 0 )
            {
                commands.Add( command );
            }

            return commands;
        }

        public List<ICommand> ParseCmdLineTokens( string requestorId, IList<string> cmdLineTokens, WriteResponseDeleg? writeRespDeleg)
        {
            var cnt = cmdLineTokens.Count();
            if (cnt == 0)
            {
                throw new MissingArgumentException("command", "A command name expected as thge 1st argument!");
            }

			var result = new List<ICommand>();

            // semicolon separates multiple commands
            var commands = ParseSubcommands( cmdLineTokens );
            foreach( var c in commands )
            {
                var cmd = ParseSingleCommand( requestorId, c );
                
                if( writeRespDeleg != null )
                {
				    cmd.Response += writeRespDeleg;
                }

				result.Add(cmd);
            }

			return result;
        }

        public List<ICommand> ParseCmdLine( string requestorId, string cmdLine, WriteResponseDeleg? writeRespDeleg )
        {
			List<string>? tokens = null;
			if( !string.IsNullOrEmpty( cmdLine ) )
			{
				SplitToWordTokens( cmdLine, out tokens );
			}
			if( tokens is { Count: > 0 } )
			{
				var cmdList = ParseCmdLineTokens( requestorId, tokens, writeRespDeleg );
				return cmdList;
			}
            return new List<ICommand>();
        }

        /// <summary>
        /// Spaces can be included inside single or double quotes.
        /// Doubled quote characters are added as a single character.
        /// Single quotes character inside single  quotes is added (the outer quotes removed)
        /// Double quote character inside single quotes is added  (the outer quotes removed)
        /// hi => hi
        /// "" => empty string token
        /// "hi" => hi
        /// "hi guys" => hi guys
        /// "hi ""guys""" => hi "guys"
        /// "hi 'guys'" => hi 'guys'
        /// 'hi "guys"' => hi "guys"
        /// 
        /// </summary>
	    public static void SplitToWordTokens( string str, out List<string> tokens )
	    {
		    tokens = new List<string>();

            int ndx = 0;
            string? s = null;
            bool insideDoubleQuote = false;
            bool insideSingleQuote = false;

            while (ndx < str.Length)
            {
                var next = str[ndx];
                var next2 = _substr(str, ndx, 2);

                if (next == ' ' && !insideDoubleQuote && !insideSingleQuote)
                {
                    if(s != null) tokens.Add(s);
                    s = null;
                    ndx++;
                }
                else
                if( next2 == "\"\"" && insideDoubleQuote)
                {
                    _append(ref s, "\"");
                    ndx += 2;
                }
                else
                if( next2 == "''" && insideSingleQuote)
                {
                    _append(ref s, "'");
                    ndx += 2;
                }
                else
                if (next == '"' && !insideSingleQuote) 
                {
                    insideDoubleQuote = !insideDoubleQuote;
                    if(s==null) s = "";
                    ndx++;
                }
                else
                if (next == '\'' && !insideDoubleQuote) 
                {
                    insideSingleQuote = !insideSingleQuote;
                    if(s==null) s = "";
                    ndx++;
                }
                else
                {
                    _append(ref s, next);
                    ndx++;
                }
            }
            if (s!=null) tokens.Add(s);
        }

	    static string _substr(string s, int index, int len )
        {
            if (index < 0) return "";
		    if (index >= s.Length) return "";
		    if (index + len > s.Length) return s.Substring(index);
		    return s.Substring(index, len);
	    }

        static void _append( ref string? s, string append )
	    {
		    if(s == null) s = append;
		    else s = s + append;
	    }

        static void _append( ref string? s, char append )
	    {
		    if(s == null) s = append.ToString();
		    else s += append;
	    }
    }
}
