﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dirigent.Agent.CmdLineCtrl
{
    public class CommandRepository
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

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

        void ProcessSingleCommand( IList<string> cmdLineTokens )
        {
            string cmdName = cmdLineTokens[0];

            ICommand cmd = Find(cmdName);
            if (cmd == null)
            {
                throw new UnknownCommandException(cmdName);
            }

            List<string> args = new List<string>(cmdLineTokens);
            args.RemoveAt(0);

            cmd.Execute(args);
        }

        List<List<string>> ParseSubcommands( IList<string> cmdLineTokens )
        {
            
            // strip the tokens containing semicolons into separate sequences of tokens ( = commands)
            List<string> newTokens = new List<string>();

            foreach( var token in cmdLineTokens )
            {
                if( token.IndexOf(";") < 0 ) // no semicolon in a token, simply add to the command 
                {
                    newTokens.Add( token.Trim() );
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

        public void ParseAndExecute(IList<string> cmdLineTokens)
        {
            var cnt = cmdLineTokens.Count();
            if (cnt == 0)
            {
                throw new MissingArgumentException("command", "A command name expected as thge 1st argument!");
            }

            // semicolon separates multiple commands
            var commands = ParseSubcommands( cmdLineTokens );
            foreach( var c in commands )
            {
                var cmdString = string.Join(" ", c); // re-assemble form tokenized form
                log.Info("Executing: "+ cmdString);

                try
                {
                    ProcessSingleCommand( c );
                }
                catch( Exception ex )
                {
                    log.Error(String.Format("Command '{0}' failed. {1}", cmdString, ex.Message) );
                }
            }
        }

    }
}
