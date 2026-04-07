using System.Drawing;
using System.Windows.Forms;

namespace Reddit98Client
{
    public sealed class StatusFooterControl
    : UserControl
    {
        private Label statusLabel;

        public StatusFooterControl()
        {
            Dock = DockStyle.Bottom;
            Height = 24;

            BuildUi();
        }

        private void BuildUi()
        {
            statusLabel =
            new Label();

            statusLabel.Dock =
            DockStyle.Fill;

            statusLabel.TextAlign =
            ContentAlignment.MiddleRight;

            statusLabel.Padding =
            new Padding(8, 0, 8, 0);

            Controls.Add(statusLabel);
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
