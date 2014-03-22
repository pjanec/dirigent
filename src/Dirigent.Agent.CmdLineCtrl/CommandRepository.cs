using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dirigent.Agent.CmdLineCtrl
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

        public void ParseAndExecute(IList<string> cmdLineTokens)
        {
            var cnt = cmdLineTokens.Count();
            if (cnt == 0)
            {
                throw new MissingArgumentException("command", "A command name expected as thge 1st argument!");
            }

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

    }
}
