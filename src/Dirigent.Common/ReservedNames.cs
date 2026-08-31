namespace Dirigent
{
	/// <summary>
	/// The values of &lt;App ExeFullPath&gt; that name something other than a program to run.
	/// </summary>
	/// <remarks>
	/// Matched case-insensitively - see Launcher.ParseExe, which is where they are acted on. Here
	/// because the config reader has to recognise one of them too, and a literal spelled in two
	/// places is a literal that will eventually differ.
	/// </remarks>
	public static class ReservedExeNames
	{
		/// <summary>The app's command line is a Dirigent command, sent to the master.</summary>
		public const string DirigentCommand = "[dirigent.command]";
	}

	/// <summary>
	/// Names used in &lt;App InitCondition&gt; that more than one assembly needs to know.
	/// </summary>
	public static class InitConditions
	{
		/// <summary>
		/// Initialized when the master's answer to this app's dirigent command has arrived -
		/// see CliResponseInitDetector. Takes a mandatory value, <see cref="Ok"/> or <see cref="Any"/>.
		/// </summary>
		public const string CliResponse = "cliresponse";

		/// <summary>Every command of the line has to have succeeded.</summary>
		public const string Ok = "ok";

		/// <summary>Any answer will do; a failure is logged and the plan carries on.</summary>
		public const string Any = "any";
	}
}
