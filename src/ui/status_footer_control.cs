using System.Drawing;
using System.Windows.Forms;

using ReW9x;
using ReW9x.Api;
using ReW9x.Models;
using ReW9x.Utils;
using OpenSslWrp;
namespace ReW9x.UI
{
    public sealed class StatusFooterControl
    : UserControl
    {
        private Label modeLabel;
        private Label statusLabel;

        public StatusFooterControl()
        {
            Dock = DockStyle.Bottom;
            Height = 24;

            BuildUi();
        }

        private void BuildUi()
        {
            modeLabel =
            new Label();

            modeLabel.Dock =
            DockStyle.Left;

            modeLabel.Width =
            220;

            modeLabel.TextAlign =
            ContentAlignment.MiddleLeft;

            modeLabel.Padding =
            new Padding(8, 0, 8, 0);

            statusLabel =
            new Label();

            statusLabel.Dock =
            DockStyle.Fill;

            statusLabel.TextAlign =
            ContentAlignment.MiddleRight;

            statusLabel.Padding =
            new Padding(8, 0, 8, 0);

            Controls.Add(modeLabel);
            Controls.Add(statusLabel);
        }

        public void SetMode(
            string text)
        {
            modeLabel.Text =
            text == null
            ? ""
            : text;
        }

        public void SetStatus(
            string text)
        {
            statusLabel.Text =
            text == null
            ? ""
            : text;
        }
    }
}
