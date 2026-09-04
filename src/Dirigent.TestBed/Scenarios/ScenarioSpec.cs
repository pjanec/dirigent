using System;
using System.Collections.Generic;

namespace Dirigent.TestBed.Scenarios
{
	/// <summary>
	/// A description of a world to test in: machines, applications, plans, file packages and the
	/// files that should already exist before the test starts.
	/// </summary>
	/// <remarks>
	/// This is plain data on purpose. One scenario is rendered several ways - into config XML for
	/// a test bed or for real processes, and into files on disk - so that a test describes the
	/// world once instead of once per tier. <see cref="Scenario"/> is the fluent way to build it.
	/// </remarks>
	public class ScenarioSpec
	{
		public List<MachineSpec> Machines = new();
		public List<AppSpec> Apps = new();
		public List<PlanSpec> Plans = new();
		public List<PackageSpec> Packages = new();
		public List<SeedSpec> Seeds = new();

		/// <summary>
		/// Escape hatch for anything not modelled here: raw XML inserted into &lt;Shared&gt;.
		/// The same placeholders as elsewhere are substituted.
		/// </summary>
		public List<string> ExtraSharedXml = new();
	}

	public class MachineSpec
	{
		public string Name = "";
		public string Ip = "127.0.0.1";

		/// <summary>share name -> path; needed only by things that build UNC paths</summary>
		public Dictionary<string, string> Shares = new();
	}

	public enum WindowStyleSpec
	{
		/// <summary>Minimized, so a test run does not throw windows at whoever is at the keyboard.</summary>
		Minimized,
		Normal,
		Maximized,
		Hidden,
	}

	public class AppSpec
	{
		public string MachineName = "";
		public string AppId = "";

		/// <summary>Null means the test application.</summary>
		public string? ExeFullPath;

		public string CmdLineArgs = "";

		/// <summary>Null means the per-application working folder the seeder creates.</summary>
		public string? StartupDir;

		/// <summary>
		/// Minimized by default: an integration test run must not interrupt whoever is using the
		/// machine. Override it only when the window style itself is what a test is about.
		/// </summary>
		public WindowStyleSpec WindowStyle = WindowStyleSpec.Minimized;

		/// <summary>Attributes written onto the App element verbatim, e.g. Volatile, RestartOnCrash.</summary>
		public Dictionary<string, string> Attributes = new();

		/// <summary>Environment variables the app should be launched with.</summary>
		public Dictionary<string, string> EnvVars = new();

		/// <summary>VFS nodes declared inside this app's section.</summary>
		public List<VfsSpec> VfsNodes = new();

		/// <summary>Raw XML inserted inside the App element.</summary>
		public List<string> ExtraXml = new();

		public AppIdTuple Id( RenderContext ctx ) => new AppIdTuple( ctx.MachineId( MachineName ), AppId );
	}

	public class PlanSpec
	{
		public string Name = "";
		public Dictionary<string, string> Attributes = new();

		/// <summary>"machine.app" of the apps in the plan, with their per-plan attributes.</summary>
		public List<PlanAppSpec> Apps = new();
	}

	public class PlanAppSpec
	{
		public string MachineName = "";
		public string AppId = "";
		public Dictionary<string, string> Attributes = new();
	}

	/// <summary>What kind of VFS node to render. Only what the tests need so far.</summary>
	public enum VfsKind
	{
		/// <summary>&lt;File Filter="Newest"&gt; over a folder</summary>
		NewestFiles,
		/// <summary>&lt;Folder&gt;</summary>
		Folder,
		/// <summary>&lt;FileRef&gt;</summary>
		Ref,
	}

	public class VfsSpec
	{
		public VfsKind Kind = VfsKind.NewestFiles;
		public string Id = "";
		public string? Title;

		/// <summary>
		/// Folder or file path. Placeholders are substituted, and {applogs} means the log folder
		/// of the application this node belongs to.
		/// </summary>
		public string? Path;

		public string? Mask;
		public int? MaxFiles;
		public double? MaxSeconds;
		public long? MaxTotalBytes;

		/// <summary>Collect only the last this many bytes of a file bigger than that.</summary>
		public long? TailBytes;

		/// <summary>Whether Clear and Mark may touch the files this node yields.</summary>
		public bool? Clearable;

		/// <summary>FileRef only: what to match. Empty string and "*" both mean "anything".</summary>
		public string? RefMachineId;
		public string? RefAppId;
	}

	public class PackageSpec
	{
		public string Id = "";
		public string? Title;
		public List<VfsSpec> Children = new();
	}

	/// <summary>A file that must exist, with a controlled age, before the test runs.</summary>
	public class SeedSpec
	{
		public string MachineName = "";

		/// <summary>Empty for a file that belongs to the machine rather than to an application.</summary>
		public string AppId = "";

		/// <summary>File name, placed in the log folder of the app (or machine).</summary>
		public string FileName = "";

		public double AgeDays;
		public int SizeBytes = 64;
		public string? Content;

		/// <summary>
		/// Fill the file with data that does not compress, so that collecting it costs what
		/// collecting a real log of that size costs. The bytes are pseudo random from a fixed seed,
		/// so two runs produce the same file.
		/// </summary>
		public bool Incompressible;
	}
}
