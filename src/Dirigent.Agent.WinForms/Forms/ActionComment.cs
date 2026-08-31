using System;
using System.Drawing;
using System.Windows.Forms;

namespace Dirigent.Gui.WinForms
{
	/// <summary>
	/// Asks the user for a note before an action runs: what is about to happen, and a box for why.
	/// </summary>
	/// <remarks>
	/// Any script action may ask for one (`AskComment="1"`), so this says nothing about what the
	/// action does - what becomes of the note is the script's business. A download writes it into the
	/// archive; another script may do something else with it, or nothing. What the operation is for
	/// is the config author's to explain, through the `Description` shown here.
	///
	/// Before rather than after, because an action can run for minutes - nobody wants to come back to
	/// a dialog - and because the answer is then available to the script itself. Built in code rather
	/// than in the designer: it is five controls, and one file is easier to read than three.
	/// </remarks>
	public class frmActionComment : Form
	{
		/// <summary>What the user typed, empty if nothing.</summary>
		public string Comment => _comment.Text.Trim();

		readonly TextBox _comment;

		/// <param name="operation">what is about to happen, i.e. the action's title</param>
		/// <param name="subject">what it happens to - a package, an app, a machine. Null if nothing in particular.</param>
		/// <param name="description">the config author's explanation, if there is one</param>
		public frmActionComment( string operation, string? subject, string? description )
		{
			var what = string.IsNullOrEmpty( subject )
						? LastSegmentOf( operation )
						: $"{LastSegmentOf( operation )} - {LastSegmentOf( subject! )}";

			Text = $"Dirigent - {what}";
			FormBorderStyle = FormBorderStyle.FixedDialog;
			StartPosition = FormStartPosition.CenterParent;
			MinimizeBox = false;
			MaximizeBox = false;
			ShowInTaskbar = false;
			ClientSize = new Size( 520, 320 );

			var heading = new Label()
			{
				Text = what,
				Location = new Point( 12, 12 ),
				Size = new Size( 496, 20 ),
				AutoSize = false,
				Font = new Font( Font, FontStyle.Bold ),
			};

			var explanation = new Label()
			{
				Text = string.IsNullOrWhiteSpace( description ) ? string.Empty : description!.Trim(),
				Location = new Point( 12, 38 ),
				Size = new Size( 496, 96 ),
				AutoSize = false,
			};

			var prompt = new Label()
			{
				Text = "A note to keep with this (optional):",
				Location = new Point( 12, 140 ),
				Size = new Size( 496, 20 ),
				AutoSize = false,
			};

			_comment = new TextBox()
			{
				Location = new Point( 12, 163 ),
				Size = new Size( 496, 105 ),
				Multiline = true,
				ScrollBars = ScrollBars.Vertical,
				AcceptsReturn = true,
			};

			var ok = new Button()
			{
				Text = "OK",
				DialogResult = DialogResult.OK,
				Location = new Point( 352, 280 ),
				Size = new Size( 75, 26 ),
			};

			var cancel = new Button()
			{
				Text = "Cancel",
				DialogResult = DialogResult.Cancel,
				Location = new Point( 433, 280 ),
				Size = new Size( 75, 26 ),
			};

			Controls.AddRange( new Control[] { heading, explanation, prompt, _comment, ok, cancel } );

			// Enter belongs to the text box, so the buttons are reached by clicking or by tabbing;
			// Escape still cancels, which is what a hurried user will reach for
			CancelButton = cancel;

			ActiveControl = _comment;
		}

		/// <summary>A title may be a menu path; the last segment is what names it.</summary>
		static string LastSegmentOf( string title )
		{
			var parts = title.Split( new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries );
			return parts.Length > 0 ? parts[parts.Length - 1] : title;
		}
	}
}
