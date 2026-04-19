using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace ReW9x.UI
{
    internal static class LinkNavigationHelper
    {
        public static void Open(
            IWin32Window owner,
            string url)
        {
            if (!IsSafeUrl(url))
                return;

            try
            {
                Process.Start(url);
            }
            catch
            {
                MessageBox.Show(
                    owner,
                    url,
                    "Could not open link",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private static bool IsSafeUrl(
            string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            if (url.IndexOf('\r') >= 0 ||
                url.IndexOf('\n') >= 0)
                return false;

            return StartsWithIgnoreCase(url, "https://") ||
                   StartsWithIgnoreCase(url, "http://");
        }

        private static bool StartsWithIgnoreCase(
            string text,
            string prefix)
        {
            if (text.Length < prefix.Length)
                return false;

            return string.Compare(
                text,
                0,
                prefix,
                0,
                prefix.Length,
                true) == 0;
        }
    }
}
