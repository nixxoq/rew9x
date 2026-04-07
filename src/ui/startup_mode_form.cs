using System;
using System.Drawing;
using System.Windows.Forms;

using ReW9x;
using ReW9x.Api;
using ReW9x.Models;
using ReW9x.Utils;
using OpenSslWrp;
namespace ReW9x.UI
{
    public enum StartupModeChoice
    {
        Anonymous,
        Login
    }

    public sealed class StartupModeForm : Form
    {
        private Label infoLabel;
        private Button loginButton;
        private Button anonymousButton;

        private StartupModeChoice _choice;

        public StartupModeForm()
        {
            _choice = StartupModeChoice.Anonymous;

            Text = "Choose Mode";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(360, 160);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            BuildUi();
        }

        public StartupModeChoice Choice
        {
            get { return _choice; }
        }

        private void BuildUi()
        {
            infoLabel = new Label();
            infoLabel.Left = 12;
            infoLabel.Top = 12;
            infoLabel.Width = 320;
            infoLabel.Height = 36;
            infoLabel.Text =
            "No saved account found. Continue anonymously or log in to Reddit.";

            loginButton = new Button();
            loginButton.Text = "Login";
            loginButton.Left = 12;
            loginButton.Top = 68;
            loginButton.Width = 120;
            loginButton.Click +=
            new EventHandler(
                LoginButton_Click);

            anonymousButton = new Button();
            anonymousButton.Text = "Anonymous";
            anonymousButton.Left = 142;
            anonymousButton.Top = 68;
            anonymousButton.Width = 120;
            anonymousButton.Click +=
            new EventHandler(
                AnonymousButton_Click);

            Controls.Add(infoLabel);
            Controls.Add(loginButton);
            Controls.Add(anonymousButton);
        }

        private void LoginButton_Click(
            object sender,
            EventArgs e)
        {
            _choice =
            StartupModeChoice.Login;

            DialogResult =
            DialogResult.OK;

            Close();
        }

        private void AnonymousButton_Click(
            object sender,
            EventArgs e)
        {
            _choice =
            StartupModeChoice.Anonymous;

            DialogResult =
            DialogResult.OK;

            Close();
        }
    }
}
