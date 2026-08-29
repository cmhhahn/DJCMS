using System.Windows;
using System.Windows.Forms;

namespace DJCMS.Views
{
    public partial class TutorialDialog : Window
    {
        public TutorialDialog()
        {
            InitializeComponent();
        }

        public string SelectedPath => PathTextBox.Text ?? string.Empty;

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new FolderBrowserDialog();
            dlg.Description = "Select your main music library folder";
            dlg.ShowNewFolderButton = true;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                PathTextBox.Text = dlg.SelectedPath;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
