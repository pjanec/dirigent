using System;
using System.Drawing;
using System.Windows.Forms;

namespace Dirigent.Gui.WinForms
{
	/// <summary>
	/// Asks the operator what to say about a collection before it starts: what is about to be
	/// collected, and a box for why.
	/// </summary>
	/// <remarks>
	/// Before rather than after, because a collection can run for minutes - nobody wants to come
	/// back to a dialog - and because the words are then available to the collection itself, which
	/// writes them into the archive. Built in code rather than in the designer: it is four controls,
	/// and one file is easier to read than three.
	/// </remarks>
	public class frmCollectionComment : Form
	{
		/// <summary>What the operator typed, empty if nothing.</summary>
		public string Comment => _comment.Text.Trim();

		readonly TextBox _comment;

		public frmCollectionComment( string title, string? description )
		{
			Text = $"Collect - {LastSegmentOf( title )}";
			FormBorderStyle = FormBorderStyle.FixedDialog;
			StartPosition = FormStartPosition.CenterParent;
			MinimizeBox = false;
			MaximizeBox = false;
			ShowInTaskbar = false;
			ClientSize = new Size( 520, 300 );

			var what = new Label()
			{
				Text = string.IsNullOrWhiteSpace( description )
						? $"About to collect: {title}"
						: description!.Trim(),
				Location = new Point( 12, 12 ),
				Size = new Size( 496, 100 ),
				AutoSize = false,
			};

			var prompt = new Label()
			{
				Text = "A note for whoever reads the archive (optional):",
				Location = new Point( 12, 120 ),
				Size = new Size( 496, 20 ),
				AutoSize = false,
			};

			_comment = new TextBox()
			{
				Location = new Point( 12, 143 ),
				Size = new Size( 496, 105 ),
				Multiline = true,
				ScrollBars = ScrollBars.Vertical,
				AcceptsReturn = true,
			};

			var ok = new Button()
			{
				Text = "Collect",
				DialogResult = DialogResult.OK,
				Location = new Point( 352, 260 ),
				Size = new Size( 75, 26 ),
			};

			var cancel = new Button()
			{
				Text = "Cancel",
				DialogResult = DialogResult.Cancel,
				Location = new Point( 433, 260 ),
				Size = new Size( 75, 26 ),
			};

			Controls.AddRange( new Control[] { what, prompt, _comment, ok, cancel } );

			// Enter belongs to the text box, so the buttons are reached by clicking or by tabbing;
			// Escape still cancels, which is what a hurried operator will reach for
			CancelButton = cancel;

			ActiveControl = _comment;
		}

		/// <summary>A node title is a menu path; the last segment is what names it.</summary>
		static string LastSegmentOf( string title )
		{
			var parts = title.Split( new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries );
			return parts.Length > 0 ? parts[parts.Length - 1] : title;
		}
	}
}
