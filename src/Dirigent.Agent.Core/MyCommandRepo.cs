using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace Dirigent
{
    /// <summary>
    /// The text commands the master understands: what each is called, which class implements it,
    /// and how to make one.
    /// </summary>
    /// <remarks>
    /// One table, so that the two things a caller may want - creating a command, and knowing how its
    /// answer ends before sending it - cannot drift apart. A repository needs a Master to create
    /// commands; asking about a response shape does not, which is why <see cref="TerminatorOf"/> is
    /// static and usable from a client that has no master at all.
    /// </remarks>
    public static class DirigentCommandRegistrator
    {
        readonly struct Entry
        {
            public readonly string Name;
            public readonly Type Type;
            public readonly CommandRepository.CmdCreatorDeleg Creator;

            public Entry( string name, Type type, CommandRepository.CmdCreatorDeleg creator )
            {
                Name = name;
                Type = type;
                Creator = creator;
            }
        }

        static readonly Entry[] _commands = new Entry[]
        {
            new( "StartPlan",          typeof( Commands.StartPlan ),          (ctrl, requestorId) => new Commands.StartPlan(ctrl, requestorId) ),
            new( "StopPlan",           typeof( Commands.StopPlan ),           (ctrl, requestorId) => new Commands.StopPlan(ctrl, requestorId) ),
            new( "KillPlan",           typeof( Commands.KillPlan ),           (ctrl, requestorId) => new Commands.KillPlan(ctrl, requestorId) ),
            new( "RestartPlan",        typeof( Commands.RestartPlan ),        (ctrl, requestorId) => new Commands.RestartPlan(ctrl, requestorId) ),
            new( "LaunchApp",          typeof( Commands.StartApp ),           (ctrl, requestorId) => new Commands.StartApp(ctrl, requestorId) ),
            new( "StartApp",           typeof( Commands.StartApp ),           (ctrl, requestorId) => new Commands.StartApp(ctrl, requestorId) ),
            new( "KillApp",            typeof( Commands.KillApp ),            (ctrl, requestorId) => new Commands.KillApp(ctrl, requestorId) ),
            new( "RestartApp",         typeof( Commands.RestartApp ),         (ctrl, requestorId) => new Commands.RestartApp(ctrl, requestorId) ),
            new( "SelectPlan",         typeof( Commands.SelectPlan ),         (ctrl, requestorId) => new Commands.SelectPlan(ctrl, requestorId) ),
            new( "GetPlanState",       typeof( Commands.GetPlanState ),       (ctrl, requestorId) => new Commands.GetPlanState(ctrl, requestorId) ),
            new( "GetAppState",        typeof( Commands.GetAppState ),        (ctrl, requestorId) => new Commands.GetAppState(ctrl, requestorId) ),
            new( "GetAllPlansState",   typeof( Commands.GetAllPlansState ),   (ctrl, requestorId) => new Commands.GetAllPlansState(ctrl, requestorId) ),
            new( "GetAllAppsState",    typeof( Commands.GetAllAppsState ),    (ctrl, requestorId) => new Commands.GetAllAppsState(ctrl, requestorId) ),
            new( "SetVars",            typeof( Commands.SetVars ),            (ctrl, requestorId) => new Commands.SetVars(ctrl, requestorId) ),
            new( "KillAll",            typeof( Commands.KillAll ),            (ctrl, requestorId) => new Commands.KillAll(ctrl, requestorId) ),
            new( "Shutdown",           typeof( Commands.Shutdown ),           (ctrl, requestorId) => new Commands.Shutdown(ctrl, requestorId) ),
            new( "Terminate",          typeof( Commands.Terminate ),          (ctrl, requestorId) => new Commands.Terminate(ctrl, requestorId) ),
            new( "Reinstall",          typeof( Commands.Reinstall ),          (ctrl, requestorId) => new Commands.Reinstall(ctrl, requestorId) ),
            new( "ReloadSharedConfig", typeof( Commands.ReloadSharedConfig ), (ctrl, requestorId) => new Commands.ReloadSharedConfig(ctrl, requestorId) ),
            new( "StartScript",        typeof( Commands.StartScript ),        (ctrl, requestorId) => new Commands.StartScript(ctrl, requestorId) ),
            new( "KillScript",         typeof( Commands.KillScript ),         (ctrl, requestorId) => new Commands.KillScript(ctrl, requestorId) ),
            new( "GetScriptState",     typeof( Commands.GetScriptState ),     (ctrl, requestorId) => new Commands.GetScriptState(ctrl, requestorId) ),
            new( "WaitForScript",      typeof( Commands.WaitForScript ),      (ctrl, requestorId) => new Commands.WaitForScript(ctrl, requestorId) ),
            new( "ApplyPlan",          typeof( Commands.ApplyPlan ),          (ctrl, requestorId) => new Commands.ApplyPlan(ctrl, requestorId) ),
            new( "GetClientState",     typeof( Commands.GetClientState ),     (ctrl, requestorId) => new Commands.GetClientState(ctrl, requestorId) ),
            new( "GetAllClientsState", typeof( Commands.GetAllClientsState ), (ctrl, requestorId) => new Commands.GetAllClientsState(ctrl, requestorId) ),
        };

        public static void Register( CommandRepository repo )
        {
            foreach( var cmd in _commands )
            {
                repo.Register( cmd.Name, cmd.Creator );
            }
        }

        /// <summary>The command names, as a client would spell them.</summary>
        public static IEnumerable<string> CommandNames => from c in _commands select c.Name;

        /// <summary>
        /// How the answer to this command ends, so that a sender knows what to wait for.
        /// </summary>
        /// <remarks>
        /// Declared by the command class itself with <see cref="CliResponseAttribute"/> - the class
        /// that writes the answer is the one that knows how it ends. An unknown command is reported
        /// as <see cref="ETerminator.Ack"/>, which is what a client waiting for one would do anyway;
        /// the master answers such a request with a single ERROR, and ERROR ends every wait.
        /// </remarks>
        public static ETerminator TerminatorOf( string commandName )
        {
            foreach( var cmd in _commands )
            {
                // deliberately the same comparison the repository itself uses when looking a
                // command up - see CommandRepository.Create
                if( cmd.Name == commandName )
                    return TerminatorOf( cmd.Type );
            }

            return ETerminator.Ack;
        }

        public static ETerminator TerminatorOf( Type commandType )
            => commandType.GetCustomAttribute<CliResponseAttribute>()?.Terminator ?? ETerminator.Ack;
    }
}
