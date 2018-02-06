using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dirigent.Common
{
    public class CommandRepository
    {
        Dictionary<string, ICommand> commands = new Dictionary<string, ICommand>();


        public void Register(ICommand command)
        {
            commands[command.Name] = command;
        }

        public ICommand Find( string name )
        {
            if (commands.ContainsKey(name))
            {
                return commands[name];
            }

            return null;
        }

        ICommand ParseSingleCommand( IList<string> cmdLineTokens )
        {
            string cmdName = cmdLineTokens[0];

            ICommand cmd = Find(cmdName);
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

        public List<ICommand> ParseCmdLineTokens(IList<string> cmdLineTokens, WriteResponseDeleg writeRespDeleg)
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
                var cmd = ParseSingleCommand( c );
				cmd.Response += writeRespDeleg;
				result.Add(cmd);
            }

			return result;
        }
    }
}
