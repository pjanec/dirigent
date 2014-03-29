using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Windows.Forms;

namespace Dirigent.Agent.Gui
{
    public class ExceptionDialog
    {
        static public void showException(Exception ex, string dialogTitle, string messageText = "")
        {
            if (messageText == "")
            {
                MessageBox.Show(
                    string.Format(
                        "{0}\n" +
                        "\n" +
                        "{1}",
                        ex.GetType().ToString(),
                        ex.Message
                    ),
                    "Dirigent - " + dialogTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
            }
            else
            {
                MessageBox.Show(
                    string.Format(
                        "{0}\n" +
                        "\n" +
                        "Exception: [{1}]\n" +
                        "{2}",
                        messageText,
                        ex.GetType().ToString(),
                        ex.Message
                    ),
                    "Dirigent - " + dialogTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
            }
        }

        static public void showExceptionWithStackTrace(Exception ex, string dialogTitle, string messageText)
        {
            MessageBox.Show(
                string.Format(
                    "{0}\n" +
                    "\n" +
                    "Exception: [{1}]\n" +
                    "{2}\n" +
                    "\n" +
                    "Stack Trace:\n{3}",
                    messageText,
                    ex.GetType().ToString(),
                    ex.Message,
                    ex.StackTrace
                ),
                "Dirigent - " + dialogTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Exclamation);
        }
    }
}
