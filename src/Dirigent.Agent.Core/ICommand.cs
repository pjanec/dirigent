using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dirigent
{
	public delegate void WriteResponseDeleg(string msg);

	public interface ICommand : IDisposable
    {

        string Name { get; }
        IList<string> Args { get; set; }
		event WriteResponseDeleg? Response;

		void Execute();
		void WriteResponse(string txt);

		/// <summary>
		/// Has this command done everything it was going to do?
		/// </summary>
		/// <remarks>
		/// The counterpart of the flag the request has always had: CLIProcessor keeps a request as
		/// long as it reports unfinished, so a command that has to wait for something - the end of a
		/// script, say - says so here and is ticked again instead of blocking the master inside
		/// Execute. Nearly every command answers and is done, hence the default.
		/// </remarks>
		bool Finished { get; }

		/// <summary>
		/// Called on each master tick after <see cref="Execute"/>, until <see cref="Finished"/>.
		/// </summary>
		/// <remarks>
		/// Execute keeps its meaning - called exactly once - so a command that does not wait is
		/// unaffected by any of this.
		/// </remarks>
		void Tick();
    }

    public class CommandNotImplementedException : Exception
    {
        public string cmdName;
        
        public CommandNotImplementedException(string cmdName)
            : base(string.Format("Command '{0}' not implemented yet.", cmdName))
        {
            this.cmdName = cmdName;
        }
    }

    public class UnknownCommandException : Exception
    {
        public string cmdName;
        
        public UnknownCommandException(string cmdName)
            : base(string.Format("Unknown command '{0}'", cmdName))
        {
            this.cmdName = cmdName;
        }
    }

    public class MissingArgumentException : Exception
    {
        public string argName;
        
        public MissingArgumentException(string argName, string details)
            : base(string.Format("Missing argument '{0}'. {1}", argName, details))
        {
            this.argName = argName;
        }
    }

    public class ArgumentSyntaxErrorException : Exception
    {
        public string argName;
        public string argValue;
        public string details;

        public ArgumentSyntaxErrorException(string argName, string argValue, string details="")
            : base(string.Format("Syntax error in argument '{0}' ('{1}') {2}", argName, argValue, details))
        {
            this.argName = argName;
            this.argValue = argValue;
            this.details = details;
        }
    }

}
