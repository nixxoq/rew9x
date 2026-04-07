using System;
using System.Drawing;
using System.Windows.Forms;

namespace Reddit98Client
{
    public sealed class ImageViewerForm
    : Form
    {
        private Panel imageScrollPanel;
        private PictureBox imageBox;
        private Image currentImage;

        public ImageViewerForm(
            Image image,
            string title)
        {
            currentImage = image;

            Text =
            string.IsNullOrEmpty(title)
            ? "Image Viewer"
            : title;

            StartPosition =
            FormStartPosition.CenterParent;

            Size =
            new Size(900, 700);

            BuildUi();
            UpdateImageLayout();
        }

        protected override void OnClosed(
            EventArgs e)
        {
            if (currentImage != null)
            {
                imageBox.Image = null;
                currentImage.Dispose();
                currentImage = null;
            }

            base.OnClosed(e);
        }

        private void BuildUi()
        {
            imageScrollPanel =
            new Panel();

            imageScrollPanel.Dock =
            DockStyle.Fill;

            imageScrollPanel.AutoScroll =
            true;

            imageScrollPanel.BackColor =
            Color.Black;

            imageScrollPanel.Resize +=
            new EventHandler(
                ImageScrollPanel_Resize);

            imageBox =
            new PictureBox();

            imageBox.Left = 0;
            imageBox.Top = 0;
            imageBox.SizeMode =
            PictureBoxSizeMode.StretchImage;
            imageBox.Image =
            currentImage;

            imageScrollPanel.Controls.Add(
                imageBox);

            Controls.Add(
                imageScrollPanel);
        }

        private void ImageScrollPanel_Resize(
            object sender,
            EventArgs e)
        {
            UpdateImageLayout();
        }

        private void UpdateImageLayout()
        {
            if (currentImage == null ||
                imageScrollPanel == null ||
                imageBox == null)
                return;

            int availableWidth =
            imageScrollPanel.ClientSize.Width - 20;

            int availableHeight =
            imageScrollPanel.ClientSize.Height - 20;

            if (availableWidth < 80)
                availableWidth = 80;

            if (availableHeight < 80)
                availableHeight = 80;

            int imageWidth =
            currentImage.Width;

            int imageHeight =
            currentImage.Height;

            int displayWidth =
            imageWidth;

            int displayHeight =
            imageHeight;

            if (displayWidth > availableWidth ||
                displayHeight > availableHeight)
            {
                int scaledByWidth =
                (imageHeight * availableWidth) /
                imageWidth;

                int scaledByHeight =
                (imageWidth * availableHeight) /
                imageHeight;

                if (scaledByWidth <= availableHeight)
                {
                    displayWidth =
                    availableWidth;

                    displayHeight =
                    scaledByWidth;
                }
                else
                {
                    displayWidth =
                    scaledByHeight;

                    displayHeight =
                    availableHeight;
                }
            }

            if (displayWidth < 1)
                displayWidth = 1;

            if (displayHeight < 1)
                displayHeight = 1;

            imageBox.Width =
            displayWidth;

            imageBox.Height =
            displayHeight;
        }
    }
}
