using System;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;

using ReW9x;
using ReW9x.Api;
using ReW9x.Models;
using ReW9x.Utils;
using OpenSslWrp;
namespace ReW9x.UI
{
    public sealed class LoginForm : Form
    {
        private readonly OAuthClient _oauth;
        private TextBox authorizeUrlBox;
        private TextBox callbackUrlBox;
        private Button copyButton;
        private Button openButton;
        private Button loginButton;
        private Label infoLabel;
        private string _state;
        private RedditToken _token;

        public LoginForm(OAuthClient oauth)
        {
            _oauth = oauth;
            Text = "Reddit Login";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(720, 260);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BuildUi();
            PrepareAuthUrl();
        }

        public RedditToken TokenResult
        {
            get { return _token; }
        }

        private void BuildUi()
        {
            infoLabel = new Label();
            infoLabel.Left = 12;
            infoLabel.Top = 12;
            infoLabel.Width = 680;
            infoLabel.Height = 36;
            infoLabel.Text = "Open the authorize URL in a browser, log in, then paste the redirected callback URL here.";

            authorizeUrlBox = new TextBox();
            authorizeUrlBox.Left = 12;
            authorizeUrlBox.Top = 50;
            authorizeUrlBox.Width = 680;
            authorizeUrlBox.ReadOnly = true;

            copyButton = new Button();
            copyButton.Text = "Copy URL";
            copyButton.Left = 12;
            copyButton.Top = 82;
            copyButton.Width = 100;
            copyButton.Click += new EventHandler(CopyButton_Click);

            openButton = new Button();
            openButton.Text = "Open";
            openButton.Left = 120;
            openButton.Top = 82;
            openButton.Width = 100;
            openButton.Click += new EventHandler(OpenButton_Click);

            callbackUrlBox = new TextBox();
            callbackUrlBox.Left = 12;
            callbackUrlBox.Top = 124;
            callbackUrlBox.Width = 680;
            callbackUrlBox.Text = "Paste callback URL here";

            loginButton = new Button();
            loginButton.Text = "Complete Login";
            loginButton.Left = 12;
            loginButton.Top = 156;
            loginButton.Width = 140;
            loginButton.Click += new EventHandler(LoginButton_Click);

            Controls.Add(infoLabel);
            Controls.Add(authorizeUrlBox);
            Controls.Add(copyButton);
            Controls.Add(openButton);
            Controls.Add(callbackUrlBox);
            Controls.Add(loginButton);
        }

        private void PrepareAuthUrl()
        {
            _state = Guid.NewGuid().ToString("N");
            authorizeUrlBox.Text = _oauth.BuildAuthorizeUrl(_state);
        }

        private void CopyButton_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(authorizeUrlBox.Text);
        }

        private void OpenButton_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start(authorizeUrlBox.Text);
            }
            catch
            {
                MessageBox.Show("Could not open a browser. Copy the URL instead.");
            }
        }

        private void LoginButton_Click(object sender, EventArgs e)
        {
            string code;
            string state;

            if (!_oauth.TryParseCallbackUrl(callbackUrlBox.Text, out code, out state))
            {
                MessageBox.Show("Could not parse callback URL.");
                return;
            }

            if (!string.Equals(state, _state, StringComparison.Ordinal))
            {
                MessageBox.Show("State mismatch.");
                return;
            }

            try
            {
                _token = _oauth.ExchangeCodeForToken(code);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Login failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
