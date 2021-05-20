using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dirigent.Common;

namespace Dirigent.Agent
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

		void SplitToWordTokens( string s, out List<string> tokens )
		{
			tokens = new List<string>();
			var parts = s.Split( null );
			foreach( var p in parts )
			{
				if( !string.IsNullOrEmpty( p ) )
				{
					tokens.Add( p );
				}
			}
		}

    }
}
